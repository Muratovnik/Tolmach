using System.Collections.Generic;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class RawCompositionTests : TestContextBase
    {
        private static Module Add(string id, string source, string target)
        {
            Module m = FixtureModule(id, "FixturePlugin");
            m.rawTexts[source] = target;
            return Activate(m);
        }

        [TestCase("Alpha\nBeta", "Альфа\nБета")]
        [TestCase("<b>Alpha</b>\n<i>Beta</i>", "<b>Альфа</b>\n<i>Бета</i>")]
        [TestCase("  Alpha \r\n\tBeta\r\n", "  Альфа \r\n\tБета\r\n")]
        [TestCase("Unknown\nBeta\nAlpha", "Unknown\nБета\nАльфа")]
        public void DifferentOwnersTranslateTheirOriginalSpans(string input, string expected)
        {
            // Register backwards: priority comes from IDs, not dictionary insertion order.
            Add("b", "Beta", "Бета");
            Add("a", "Alpha", "Альфа");
            RawDisplay.Initialize();
            Assert.That(RawDisplay.Translate(input), Is.EqualTo(expected));
            Assert.That(RawDisplay.Translate(input), Is.EqualTo(expected), "The cached result must include every original fragment.");
        }

        [Test]
        public void OutputFromOneOwnerNeverBecomesInputToAnother()
        {
            Add("a", "Alpha", "Beta");
            Add("b", "Beta", "Бета");
            RawDisplay.Initialize();
            Assert.That(RawDisplay.Translate("Alpha\nBeta"), Is.EqualTo("Beta\nБета"));
            Assert.That(RawDisplay.Translate("<b>Alpha</b>"), Is.EqualTo("<b>Beta</b>"));
        }

        [Test]
        public void AWholeMessageInALaterOwnerPrecedesEarlierPartialMatches()
        {
            Add("a", "Alpha", "Альфа");
            Add("b", "Alpha\nBeta", "Целое сообщение");
            RawDisplay.Initialize();
            Assert.That(RawDisplay.Translate("Alpha\nBeta"), Is.EqualTo("Целое сообщение"));
        }

        [Test]
        public void TemplatesKeepTheirOwnVocabulariesDuringComposition()
        {
            Add("a", "Alpha", "Альфа");
            Module b = FixtureModule("b", "FixturePlugin");
            b.terms["kind"] = new Dictionary<string, string> { { "Bow", "Лук" } };
            b.rawPatterns.Add(new PatternSpec { source = "Item: {0}", target = "Предмет: {0}",
                arguments = new Dictionary<string, string> { { "0", "term:kind" } } });
            Activate(b);
            RawDisplay.Initialize();
            Assert.That(RawDisplay.Translate("Alpha\nItem: Bow"), Is.EqualTo("Альфа\nПредмет: Лук"));
            Assert.That(RawDisplay.Translate("Alpha\nItem: Unknown"), Is.EqualTo("Альфа\nItem: Unknown"));
        }

        [Test]
        public void ReinitializationDropsCachedResultsForRemovedModules()
        {
            Add("a", "Alpha", "Альфа");
            Add("b", "Beta", "Бета");
            RawDisplay.Initialize();
            Assert.That(RawDisplay.Translate("Alpha\nBeta"), Is.EqualTo("Альфа\nБета"));
            TextEngine.Modules.Remove("b");
            RawDisplay.Initialize();
            Assert.That(RawDisplay.Translate("Alpha\nBeta"), Is.EqualTo("Альфа\nBeta"));
        }
    }
}
