// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ClassicUO.Agent
{
    /// <summary>
    /// A rough census of the managed heap by reflection, for finding what a headless client holds
    /// that it does not need: walks everything reachable from the static fields of the client's own
    /// assemblies and charges each object to the first root that reaches it, and to its type.
    /// Sizes are estimates (header plus fields, arrays by length). Slow - seconds - and racy, since
    /// it reads off the game thread; a diagnostic, never something to run in a loop.
    /// </summary>
    internal static class MemCensus
    {
        private sealed class Tally
        {
            public long Bytes, Objects;
        }

        private static readonly Dictionary<Type, FieldInfo[]> _fields = new();
        private static readonly Dictionary<Type, long> _sizes = new();
        private static readonly Dictionary<Type, bool> _hasRefs = new();

        public static List<string> Run(int top, long maxObjects)
        {
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var roots = new Dictionary<string, Tally>();
            var types = new Dictionary<Type, Tally>();
            var stack = new Stack<object>();
            long count = 0, total = 0;
            bool capped = false;
            var started = DateTime.UtcNow;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name ?? "";

                if (!an.StartsWith("ClassicUO") && an != "cuo" && !an.StartsWith("FNA"))
                {
                    continue;
                }

                Type[] ts;

                try
                {
                    ts = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    ts = e.Types.Where(t => t != null).ToArray();
                }

                foreach (var t in ts)
                {
                    if (t.ContainsGenericParameters)
                    {
                        continue;
                    }

                    foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsLiteral || f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType.IsPointer || f.FieldType.IsByRefLike ||
                            f.IsDefined(typeof(ThreadStaticAttribute)))
                        {
                            continue;
                        }

                        object v;

                        try
                        {
                            v = f.GetValue(null);
                        }
                        catch
                        {
                            continue;
                        }

                        if (v == null)
                        {
                            continue;
                        }

                        var tally = new Tally();
                        stack.Push(v);

                        while (stack.Count > 0)
                        {
                            var o = stack.Pop();

                            if (!seen.Add(o))
                            {
                                continue;
                            }

                            if (++count > maxObjects)
                            {
                                capped = true;
                                stack.Clear();

                                break;
                            }

                            var ot = o.GetType();
                            long size = SizeOf(o, ot);
                            tally.Bytes += size;
                            tally.Objects++;
                            total += size;

                            if (!types.TryGetValue(ot, out var tt))
                            {
                                types[ot] = tt = new Tally();
                            }

                            tt.Bytes += size;
                            tt.Objects++;

                            if (o is string || o is Type || o is MemberInfo || o is Assembly || o is Module)
                            {
                                continue;
                            }

                            if (o is Delegate d)
                            {
                                if (d.Target != null)
                                {
                                    stack.Push(d.Target);
                                }

                                continue;
                            }

                            if (o is Array a)
                            {
                                var et = ot.GetElementType();

                                if (et == null || et.IsPrimitive || et.IsEnum || et.IsPointer || (et.IsValueType && !HasRefs(et)))
                                {
                                    continue;
                                }

                                foreach (var e in a)
                                {
                                    if (e == null)
                                    {
                                        continue;
                                    }

                                    if (et.IsValueType)
                                    {
                                        PushFields(e, et, stack);
                                    }
                                    else
                                    {
                                        stack.Push(e);
                                    }
                                }

                                continue;
                            }

                            PushFields(o, ot, stack);
                        }

                        if (tally.Objects > 0)
                        {
                            roots[$"{t.FullName}.{f.Name}"] = tally;
                        }

                        if (capped)
                        {
                            break;
                        }
                    }

                    if (capped)
                    {
                        break;
                    }
                }

                if (capped)
                {
                    break;
                }
            }

            var gc = GC.GetGCMemoryInfo();
            long mb(long b) => b / (1024 * 1024);
            var lines = new List<string>
            {
                $"census: {count:N0} objects, about {mb(total)} MB reachable from static roots, {(DateTime.UtcNow - started).TotalSeconds:F1} s{(capped ? " (CAPPED)" : "")}; " +
                $"GC heap {mb(gc.HeapSizeBytes)} MB",
                "gen sizes (MB): " + string.Join(", ", gc.GenerationInfo.ToArray().Select((g, i) => $"{(i == 3 ? "LOH" : i == 4 ? "POH" : "gen" + i)} {mb(g.SizeAfterBytes)}")),
                $"-- top {top} roots --"
            };

            lines.AddRange(roots.OrderByDescending(r => r.Value.Bytes).Take(top).Select(r => $"{mb(r.Value.Bytes),6} MB {r.Value.Objects,10:N0} obj  {r.Key}"));
            lines.Add($"-- top {top} types --");
            lines.AddRange(types.OrderByDescending(r => r.Value.Bytes).Take(top).Select(r => $"{mb(r.Value.Bytes),6} MB {r.Value.Objects,10:N0} obj  {r.Key.FullName}"));

            return lines;
        }

        private static void PushFields(object o, Type t, Stack<object> stack)
        {
            foreach (var f in FieldsOf(t))
            {
                object v;

                try
                {
                    v = f.GetValue(o);
                }
                catch
                {
                    continue;
                }

                if (v == null)
                {
                    continue;
                }

                if (f.FieldType.IsValueType)
                {
                    PushFields(v, f.FieldType, stack);    // an inline struct: its references, not the box
                }
                else
                {
                    stack.Push(v);
                }
            }
        }

        /// <summary>The instance fields of a type, base types included, that can lead somewhere: references, and structs that hold references.</summary>
        private static FieldInfo[] FieldsOf(Type t)
        {
            if (_fields.TryGetValue(t, out var fs))
            {
                return fs;
            }

            var list = new List<FieldInfo>();

            for (var bt = t; bt != null && bt != typeof(object); bt = bt.BaseType)
            {
                foreach (var f in bt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var ft = f.FieldType;

                    if (ft.IsPrimitive || ft.IsEnum || ft.IsPointer || ft.IsByRefLike || (ft.IsValueType && !HasRefs(ft)))
                    {
                        continue;
                    }

                    list.Add(f);
                }
            }

            return _fields[t] = list.ToArray();
        }

        private static bool HasRefs(Type t)
        {
            if (_hasRefs.TryGetValue(t, out var r))
            {
                return r;
            }

            try
            {
                r = (bool)typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences))!.MakeGenericMethod(t).Invoke(null, null)!;
            }
            catch
            {
                r = true;
            }

            return _hasRefs[t] = r;
        }

        private static long ValueSize(Type t)
        {
            if (!t.IsValueType || t.IsPointer)
            {
                return IntPtr.Size;
            }

            if (_sizes.TryGetValue(t, out var s))
            {
                return s;
            }

            try
            {
                s = (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(t).Invoke(null, null)!;
            }
            catch
            {
                s = IntPtr.Size;
            }

            return _sizes[t] = s;
        }

        private static readonly Dictionary<Type, long> _objectSizes = new();

        private static long SizeOf(object o, Type t)
        {
            if (o is string str)
            {
                return 22 + 2L * str.Length;
            }

            if (o is Array a)
            {
                var et = t.GetElementType();

                return 24 + a.LongLength * (et == null ? IntPtr.Size : ValueSize(et));
            }

            if (t.IsValueType)
            {
                return 16 + ValueSize(t);
            }

            if (_objectSizes.TryGetValue(t, out var s))
            {
                return s;
            }

            s = 16;

            for (var bt = t; bt != null && bt != typeof(object); bt = bt.BaseType)
            {
                foreach (var f in bt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    s += f.FieldType.IsValueType ? ValueSize(f.FieldType) : IntPtr.Size;
                }
            }

            return _objectSizes[t] = Math.Max(24, s);
        }
    }
}
