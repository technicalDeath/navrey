using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.Game
{
    public class LegacyMenuOptionsTests
    {
        [Fact]
        public void UsesServerIndexAndArtRatherThanListPosition()
        {
            var options = new LegacyMenuOptions("Craft");
            options.Add(3, 0x1234, 0x56, "Scissors");
            Assert.False(options.Respond(1, (_, _, _) => Assert.Fail("must not send")));
            Assert.True(options.Respond(3, (index, graphic, hue) =>
            {
                Assert.Equal(3, index);
                Assert.Equal(0x1234, graphic);
                Assert.Equal(0x56, hue);
            }));
            Assert.False(options.Respond(3, (_, _, _) => Assert.Fail("must not send twice")));
        }

        [Fact]
        public void CancelUsesZeroPacketFieldsAndAnswersOnlyOnce()
        {
            var options = new LegacyMenuOptions("Craft");
            Assert.True(options.Respond(0, (index, graphic, hue) =>
            {
                Assert.Equal(0, index);
                Assert.Equal(0, graphic);
                Assert.Equal(0, hue);
            }));
            Assert.False(options.Respond(0, (_, _, _) => Assert.Fail("must not send twice")));
        }
    }
}
