using Game.Core.Tests.Sim;
using Game.Core.Text;
using NUnit.Framework;

namespace Game.Core.Tests
{
    [SetUpFixture]
    public class TextSetUp
    {
        [OneTimeSetUp]
        public void LoadStrings()
        {
            Texts.Use(TestWorld.Real.Strings);
            Texts.UseLanguage(StringTable.DefaultLanguage);
        }
    }
}
