// SPDX-License-Identifier: BSD-2-Clause
using System;
using System.Globalization;
using System.Linq;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Agent
{
    internal sealed partial class CommandEngine
    {
        private static LegacyMenuOptions MenuOptions(Gump gump) => gump switch
        {
            MenuGump menu => menu.Options,
            GrayMenuGump menu => menu.Options,
            _ => null
        };

        private static bool TryMenuId(string text, out uint value) => uint.TryParse(
            text != null && text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.Substring(2) : text,
            NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        private void RegisterLegacyMenus()
        {
            Register("menus", "menus", "List open legacy item-list and gray menus with server option indices", ctx =>
            {
                var lines = ctx.Game(_ => UIManager.Gumps
                    .Where(g => !g.IsDisposed && MenuOptions(g) != null)
                    .SelectMany(g => new[] { $"[MENU] local 0x{g.LocalSerial:X8} server 0x{g.ServerSerial:X8} {MenuOptions(g).Title}" }
                        .Concat(MenuOptions(g).Entries.Select(e =>
                            $"  [option {e.Index}] graphic 0x{e.Graphic:X4} hue 0x{e.Hue:X4} {e.Text}")))
                    .ToList());
                if (lines.Count == 0) ctx.Print("No legacy menus open");
                foreach (var line in lines) ctx.Print(line);
            });

            Register("menuresponse", "menuresponse <localSerial> <serverMenuId> <index>",
                "Select a listed legacy menu option (0 cancels); use both hex IDs from menus", ctx =>
            {
                if (ctx.ArgCount != 3 || !TryMenuId(ctx.Arg(0), out uint local) ||
                    !TryMenuId(ctx.Arg(1), out uint server) || server > ushort.MaxValue ||
                    !int.TryParse(ctx.Arg(2), out int index) || index < 0)
                {
                    ctx.Fail("usage: menuresponse <localSerial> <serverMenuId> <index>; copy both hex IDs and an option from menus");
                    return;
                }

                string error = ctx.Game(_ =>
                {
                    var menus = UIManager.Gumps.Where(g => !g.IsDisposed && MenuOptions(g) != null &&
                        g.LocalSerial == local && g.ServerSerial == server).ToList();
                    if (menus.Count != 1) return "menu missing or ambiguous; read menus again";
                    bool sent = menus[0] switch
                    {
                        MenuGump menu => menu.Respond(index),
                        GrayMenuGump menu => menu.Respond(index),
                        _ => false
                    };
                    return sent ? null : "option is absent or menu already answered; read menus again";
                });
                if (error != null) ctx.Fail(error);
                else ctx.Print($"Menu option {index} sent; verify the resulting menu or inventory");
            });
        }
    }
}
