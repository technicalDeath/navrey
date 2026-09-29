// SPDX-License-Identifier: BSD-2-Clause
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Game.UI.Gumps
{
    // Keep the server's indices: missing art can leave gaps in the visible menu.
    internal sealed class LegacyMenuOptions
    {
        internal record Entry(int Index, ushort Graphic, ushort Hue, string Text);
        private readonly List<Entry> _entries = new();
        private bool _answered;

        public LegacyMenuOptions(string title) => Title = title;
        public string Title { get; }
        public IReadOnlyList<Entry> Entries => _entries;
        public void Add(int index, ushort graphic, ushort hue, string text) =>
            _entries.Add(new Entry(index, graphic, hue, text));

        public bool Respond(int index, Action<int, ushort, ushort> send)
        {
            if (_answered) return false;
            Entry entry = index == 0 ? new Entry(0, 0, 0, "Cancel") : _entries.FirstOrDefault(e => e.Index == index);
            if (entry == null) return false;
            send(entry.Index, entry.Graphic, entry.Hue);
            _answered = true;
            return true;
        }
    }
}
