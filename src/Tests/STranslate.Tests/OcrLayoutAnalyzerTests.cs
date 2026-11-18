using STranslate.Core;
using STranslate.Plugin;
using System.Text.Json;
using System.Windows.Controls;

namespace STranslate.Tests;

public class OcrLayoutAnalyzerTests
{
    [Fact]
    public void SmartMergesParagraphLines()
    {
        var result = AnalyzeSmart(
            Box("This is the first line", 0, 0, 180, 20),
            Box("continued on the next line", 0, 24, 210, 20));

        Assert.Single(result);
        Assert.Equal("This is the first line continued on the next line", result[0].Text);
    }

    [Fact]
    public void SmartKeepsColumnsSeparate()
    {
        var result = AnalyzeSmart(
            Box("Left column starts here", 0, 0, 180, 20),
            Box("Right column starts here", 300, 0, 190, 20),
            Box("and continues below", 0, 24, 160, 20),
            Box("with its own text", 300, 24, 150, 20));

        Assert.Equal(2, result.Count);
        Assert.Equal("Left column starts here and continues below", result[0].Text);
        Assert.Equal("Right column starts here with its own text", result[1].Text);
    }

    [Fact]
    public void SmartCompletesLeftColumnBeforeRightColumn()
    {
        var result = AnalyzeSmart(
            Box("Left first paragraph", 0, 0, 170, 20),
            Box("continues here", 0, 24, 135, 20),
            Box("Right column starts", 300, 12, 170, 20),
            Box("continues separately", 300, 36, 180, 20),
            Box("Left second paragraph", 0, 70, 185, 20),
            Box("continues too", 0, 94, 120, 20));

        Assert.Equal(3, result.Count);
        Assert.Equal("Left first paragraph continues here", result[0].Text);
        Assert.Equal("Left second paragraph continues too", result[1].Text);
        Assert.Equal("Right column starts continues separately", result[2].Text);
    }

    [Fact]
    public void SmartDoesNotMergeUiLabelsOnSameRow()
    {
        var result = AnalyzeSmart(
            Box("File", 0, 0, 32, 20),
            Box("Edit", 66, 0, 32, 20),
            Box("View", 132, 0, 36, 20));

        Assert.Equal(["File", "Edit", "View"], result.Select(x => x.Text));
    }

    [Fact]
    public void SmartDoesNotMergeSettingsCardControls()
    {
        var result = AnalyzeSmart(
            Box("General", 0, 0, 64, 20),
            Box("Enable", 220, 0, 70, 20),
            Box("Theme", 0, 32, 56, 20),
            Box("Dark", 220, 32, 46, 20));

        Assert.Equal(["General", "Theme", "Enable", "Dark"], result.Select(x => x.Text));
    }

    [Fact]
    public void SmartDoesNotMergeTableCells()
    {
        var result = AnalyzeSmart(
            Box("First name", 0, 0, 100, 20),
            Box("Order status", 150, 0, 110, 20),
            Box("Alice Smith", 0, 28, 105, 20),
            Box("Active now", 150, 28, 95, 20),
            Box("Bob Stone", 0, 56, 90, 20),
            Box("Paused now", 150, 56, 96, 20));

        Assert.Equal(
            ["First name", "Alice Smith", "Bob Stone", "Order status", "Active now", "Paused now"],
            result.Select(x => x.Text));
    }

    [Fact]
    public void SmartKeepsPowerToysStyleGridItemsSeparate()
    {
        var result = AnalyzeSmart(
            Box("Advanced Paste", 0, 0, 190, 28),
            Box("Always on Top", 320, 0, 190, 28),
            Box("Awake", 760, 0, 95, 28),
            Box("Color Picker", 0, 44, 165, 28),
            Box("Command Not Found", 320, 44, 260, 28),
            Box("Command Palette", 760, 44, 230, 28),
            Box("Crop And Lock", 0, 88, 185, 28),
            Box("Environment Variables", 320, 88, 285, 28),
            Box("FancyZones", 760, 88, 160, 28),
            Box("File Explorer Add-ons", 0, 132, 270, 28),
            Box("File Locksmith", 320, 132, 190, 28),
            Box("Grab And Move", 760, 132, 210, 28),
            Box("Hosts File Editor", 0, 176, 210, 28),
            Box("Image Resizer", 320, 176, 180, 28),
            Box("Keyboard Manager", 760, 176, 230, 28),
            Box("Light Switch", 0, 220, 155, 28),
            Box("Mouse Utilities", 320, 220, 205, 28),
            Box("Mouse Without Borders", 760, 220, 285, 28));

        var texts = result.Select(x => x.Text).ToList();
        var expected = new[]
        {
            "Advanced Paste",
            "Always on Top",
            "Awake",
            "Color Picker",
            "Command Not Found",
            "Command Palette",
            "Crop And Lock",
            "Environment Variables",
            "FancyZones",
            "File Explorer Add-ons",
            "File Locksmith",
            "Grab And Move",
            "Hosts File Editor",
            "Image Resizer",
            "Keyboard Manager",
            "Light Switch",
            "Mouse Utilities",
            "Mouse Without Borders"
        };

        Assert.Equal(expected.Length, texts.Count);
        Assert.All(expected, text => Assert.Contains(text, texts));
        Assert.DoesNotContain(texts, text => text.Contains("File Explorer Add-ons File Locksmith"));
        Assert.DoesNotContain(texts, text => text.Contains("File Explorer Add-ons Hosts File Editor Light Switch"));
        Assert.DoesNotContain(texts, text => text.Contains("Grab And Move Keyboard Manager"));
    }

    [Fact]
    public void SmartKeepsTableRowFragmentsTogetherWithoutMergingRows()
    {
        var result = AnalyzeSmart(
            Box("File", 0, 0, 34, 24),
            Box("Explorer Add-ons", 42, 0, 162, 24),
            Box("Mouse", 300, 0, 64, 24),
            Box("Without Borders", 372, 0, 170, 24),
            Box("Command", 600, 0, 106, 24),
            Box("Palette", 714, 0, 80, 24),
            Box("Hosts", 0, 40, 58, 24),
            Box("File Editor", 66, 40, 104, 24),
            Box("Image", 300, 40, 66, 24),
            Box("Resizer", 374, 40, 76, 24),
            Box("PowerToys", 600, 40, 118, 24),
            Box("Run", 726, 40, 42, 24),
            Box("Light", 0, 80, 54, 24),
            Box("Switch", 62, 80, 68, 24),
            Box("Screen", 300, 80, 74, 24),
            Box("Ruler", 382, 80, 58, 24),
            Box("Quick", 600, 80, 62, 24),
            Box("Accent", 670, 80, 76, 24));

        Assert.Equal(9, result.Count);
        Assert.Contains(result, block => block.Text == "File Explorer Add-ons");
        Assert.Contains(result, block => block.Text == "Mouse Without Borders");
        Assert.Contains(result, block => block.Text == "Command Palette");
        Assert.DoesNotContain(result, block => block.Text.Contains("File Explorer Add-ons Hosts File Editor Light Switch"));
    }

    [Fact]
    public void SmartKeepsTableLeadingIconsWithText()
    {
        var result = AnalyzeSmart(
            Box("*", 0, 0, 20, 24),
            Box("Advanced Paste", 44, 0, 154, 24),
            Box("*", 300, 0, 20, 24),
            Box("Always on Top", 344, 0, 148, 24),
            Box("*", 0, 40, 20, 24),
            Box("Color Picker", 44, 40, 128, 24),
            Box("*", 300, 40, 20, 24),
            Box("Command Palette", 344, 40, 184, 24),
            Box("*", 0, 80, 20, 24),
            Box("File Explorer Add-ons", 44, 80, 212, 24),
            Box("*", 300, 80, 20, 24),
            Box("File Locksmith", 344, 80, 144, 24));

        Assert.Equal(6, result.Count);
        Assert.Contains(result, block => block.Text == "*Advanced Paste");
        Assert.Contains(result, block => block.Text == "*Always on Top");
        Assert.Contains(result, block => block.Text == "*File Explorer Add-ons");
        Assert.DoesNotContain(result, block => block.Text == "*");
    }

    [Fact]
    public void SmartKeepsThreeLineMultiColumnBodyMerged()
    {
        var result = AnalyzeSmart(
            Box("Left column starts here", 0, 0, 210, 20),
            Box("Right column starts here", 330, 0, 220, 20),
            Box("and continues below", 0, 24, 180, 20),
            Box("with its own text", 330, 24, 170, 20),
            Box("before ending normally", 0, 48, 210, 20),
            Box("over multiple lines", 330, 48, 180, 20));

        Assert.Equal(2, result.Count);
        Assert.Equal("Left column starts here and continues below before ending normally", result[0].Text);
        Assert.Equal("Right column starts here with its own text over multiple lines", result[1].Text);
    }

    [Fact]
    public void SmartKeepsThreeColumnBodyMerged()
    {
        var result = AnalyzeSmart(
            Box("Column one starts here", 0, 0, 205, 20),
            Box("Column two starts here", 300, 0, 205, 20),
            Box("Column three starts here", 600, 0, 225, 20),
            Box("and carries the thought", 0, 24, 210, 20),
            Box("with the next sentence", 300, 24, 210, 20),
            Box("through another line", 600, 24, 185, 20),
            Box("before ending normally", 0, 48, 210, 20),
            Box("inside the same column", 300, 48, 215, 20),
            Box("without table spacing", 600, 48, 195, 20));

        Assert.Equal(3, result.Count);
        Assert.Equal("Column one starts here and carries the thought before ending normally", result[0].Text);
        Assert.Equal("Column two starts here with the next sentence inside the same column", result[1].Text);
        Assert.Equal("Column three starts here through another line without table spacing", result[2].Text);
    }

    [Fact]
    public void SmartKeepsTitleAndBodySeparate()
    {
        var result = AnalyzeSmart(
            Box("Account Settings", 0, 0, 220, 32),
            Box("Manage your profile details", 0, 48, 230, 20));

        Assert.Equal(2, result.Count);
        Assert.Equal("Account Settings", result[0].Text);
        Assert.Equal("Manage your profile details", result[1].Text);
    }

    [Fact]
    public void SmartKeepsListItemsAndMergesContinuation()
    {
        var result = AnalyzeSmart(
            Box("- First item", 0, 0, 90, 20),
            Box("continued detail", 24, 24, 130, 20),
            Box("- Second item", 0, 48, 105, 20));

        Assert.Equal(2, result.Count);
        Assert.Equal("- First item continued detail", result[0].Text);
        Assert.Equal("- Second item", result[1].Text);
    }

    /// <summary>
    /// 验证 OCR 将整个编号列表装入单个文本框时，Smart 仍按连续编号恢复独立条目。
    /// </summary>
    [Fact]
    public void SmartSplitsEmbeddedNumberedListItems()
    {
        var result = AnalyzeSmart(
            Box(
                """
                1. add filesharehelper.peekdata for back host to read flatness wfm
                   without deleting the shared file
                2. compute flatnessresult in host.back calculate alongside ntresults
                3. remove local flatnessresult call from front host calculate
                4. merge flatnessresult in calculateprocessor and switch match key
                   from metrorecipeid to metrorecipename
                5. set metrorecipename explicitly on both sides to fix the merge match
                   that also affected ntresults
                """,
                0,
                0,
                510,
                160));

        Assert.Equal(5, result.Count);
        Assert.Equal(
            "1. add filesharehelper.peekdata for back host to read flatness wfm without deleting the shared file",
            result[0].Text);
        Assert.Equal(
            "2. compute flatnessresult in host.back calculate alongside ntresults",
            result[1].Text);
        Assert.Equal(
            "3. remove local flatnessresult call from front host calculate",
            result[2].Text);
        Assert.Equal(
            "4. merge flatnessresult in calculateprocessor and switch match key from metrorecipeid to metrorecipename",
            result[3].Text);
        Assert.Equal(
            "5. set metrorecipename explicitly on both sides to fix the merge match that also affected ntresults",
            result[4].Text);
    }

    /// <summary>
    /// 验证普通多行 OCR 块不会因块内换行被激进拆分。
    /// </summary>
    [Fact]
    public void SmartKeepsOrdinaryMultilineOcrBlockIntact()
    {
        const string text = "A regular paragraph starts here\nand continues inside one OCR block.";

        var result = AnalyzeSmart(Box(text, 0, 0, 420, 48));

        Assert.Single(result);
        Assert.Equal(text, result[0].Text);
    }

    /// <summary>
    /// 验证不连续的数字行不会被误判为明确的有序列表。
    /// </summary>
    [Fact]
    public void SmartKeepsNonSequentialNumberedLinesIntact()
    {
        const string text = "2024. Previous release summary\n2026. Current release outlook";

        var result = AnalyzeSmart(Box(text, 0, 0, 360, 48));

        Assert.Single(result);
        Assert.Equal(text, result[0].Text);
    }

    [Fact]
    public void SmartAddsSpacesForLatinWordLevelOcr()
    {
        var result = AnalyzeSmart(
            Box("Hello", 0, 0, 42, 20),
            Box("world", 50, 0, 45, 20));

        Assert.Single(result);
        Assert.Equal("Hello world", result[0].Text);
    }

    [Fact]
    public void SmartAvoidsSpacesForCjkWordLevelOcr()
    {
        var result = AnalyzeSmart(
            Box("你", 0, 0, 20, 20),
            Box("好", 22, 0, 20, 20));

        Assert.Single(result);
        Assert.Equal("你好", result[0].Text);
    }

    [Fact]
    public void SmartMergesHyphenatedEnglishContinuation()
    {
        var result = AnalyzeSmart(
            Box("trans-", 0, 0, 54, 20),
            Box("lation", 0, 24, 56, 20));

        Assert.Single(result);
        Assert.Equal("translation", result[0].Text);
    }

    [Fact]
    public void SmartSplitsPdfBodyParagraphsOnBlankLineGaps()
    {
        var result = AnalyzeSmart(
            Box("Namespaces with synchronization capability provide", 0, 0, 500, 30),
            Box("two additional attributes, SynchOn and SynchFail.", 0, 36, 500, 30),
            Box("Synchronization for a new or changed recipe,", 0, 94, 500, 30),
            Box("recipe form, consists of uploading the execution recipe.", 0, 130, 500, 30),
            Box("The recipe executor saves the last value", 0, 188, 500, 30),
            Box("parameter in the execution recipe attribute.", 0, 224, 500, 30));

        Assert.Equal(3, result.Count);
        Assert.Equal(
            "Namespaces with synchronization capability provide two additional attributes, SynchOn and SynchFail.",
            result[0].Text);
        Assert.Equal(
            "Synchronization for a new or changed recipe,recipe form, consists of uploading the execution recipe.",
            result[1].Text);
        Assert.Equal(
            "The recipe executor saves the last value parameter in the execution recipe attribute.",
            result[2].Text);
    }

    [Fact]
    public void SmartSplitsAfterSentenceEndingWithLargerGap()
    {
        var result = AnalyzeSmart(
            Box("The first paragraph ends here.", 0, 0, 420, 30),
            Box("Another paragraph starts with an uppercase word.", 0, 56, 500, 30));

        Assert.Equal(2, result.Count);
        Assert.Equal("The first paragraph ends here.", result[0].Text);
        Assert.Equal("Another paragraph starts with an uppercase word.", result[1].Text);
    }

    [Fact]
    public void SmartSplitsAfterShortLineReturningToBodyLeft()
    {
        var result = AnalyzeSmart(
            Box("which synchronization failed.", 0, 0, 260, 30),
            Box("Synchronization for a new recipe starts here", 0, 56, 500, 30));

        Assert.Equal(2, result.Count);
        Assert.Equal("which synchronization failed.", result[0].Text);
        Assert.Equal("Synchronization for a new recipe starts here", result[1].Text);
    }

    [Fact]
    public void SmartSplitsAfterShortLastLineRelativeToParagraphWidth()
    {
        var result = AnalyzeSmart(
            Box("the first paragraph keeps a regular body width", 0, 0, 520, 30),
            Box("and continues with the same regular body width", 0, 36, 520, 30),
            Box("before ending shorter", 0, 72, 360, 30),
            Box("another paragraph starts with narrower text", 0, 126, 400, 30),
            Box("and continues on the next body line", 0, 162, 500, 30));

        Assert.Equal(2, result.Count);
        Assert.Equal(
            "the first paragraph keeps a regular body width and continues with the same regular body width before ending shorter",
            result[0].Text);
        Assert.Equal(
            "another paragraph starts with narrower text and continues on the next body line",
            result[1].Text);
    }

    [Fact]
    public void SmartSplitsIndentedParagraphAfterShortLastLineWithoutBlankLine()
    {
        var result = AnalyzeSmart(
            Box("first paragraph starts with an indent", 24, 0, 420, 30),
            Box("continues on the full body width", 0, 36, 500, 30),
            Box("ends on a short last line", 0, 72, 240, 30),
            Box("next paragraph starts indented", 24, 108, 430, 30),
            Box("continues with body text", 0, 144, 490, 30));

        Assert.Equal(2, result.Count);
        Assert.Equal(
            "first paragraph starts with an indent continues on the full body width ends on a short last line",
            result[0].Text);
        Assert.Equal(
            "next paragraph starts indented continues with body text",
            result[1].Text);
    }

    [Fact]
    public void ApplyLeavesContentsWithoutBoxPointsUnchanged()
    {
        var ocrResult = new OcrResult
        {
            OcrContents =
            [
                new() { Text = "plain text" },
                new() { Text = "second line" }
            ]
        };

        OcrLayoutAnalyzer.Apply(ocrResult, LayoutAnalysisMode.Smart);

        Assert.Equal(["plain text", "second line"], ocrResult.OcrContents.Select(x => x.Text));
    }

    [Fact]
    public void NoMergePreservesOriginalBlocks()
    {
        var result = OcrLayoutAnalyzer.Analyze(
            [
                Box("One", 0, 0, 40, 20),
                Box("Two", 0, 24, 40, 20)
            ],
            LayoutAnalysisMode.NoMerge);

        Assert.Equal(["One", "Two"], result.Select(x => x.Text));
    }

    [Fact]
    public void SettingsReadsUnknownLayoutAnalysisModeAsAuto()
    {
        var settings = JsonSerializer.Deserialize<Settings>(
            """{"LayoutAnalysisMode":"standardDocument"}""")!;

        Assert.Equal(LayoutAnalysisMode.Auto, settings.LayoutAnalysisMode);
    }

    [Fact]
    public void SettingsReadsSmartLayoutAnalysisModeAsSmart()
    {
        var settings = JsonSerializer.Deserialize<Settings>(
            """{"LayoutAnalysisMode":"smart"}""")!;

        Assert.Equal(LayoutAnalysisMode.Smart, settings.LayoutAnalysisMode);
    }

    [Fact]
    public void SettingsKeepsNoMergeLayoutAnalysisMode()
    {
        var settings = new Settings { LayoutAnalysisMode = LayoutAnalysisMode.NoMerge };

        settings.NormalizeLayoutAnalysisMode();

        Assert.Equal(LayoutAnalysisMode.NoMerge, settings.LayoutAnalysisMode);
    }

    [Fact]
    public void AutoUsesProviderLayoutWhenAvailable()
    {
        var result = new OcrResult
        {
            OcrContents = [Box("Flat fallback", 0, 100, 100, 20)],
            Regions =
            [
                new()
                {
                    Paragraphs =
                    [
                        new()
                        {
                            Lines =
                            [
                                Box("Provider first line", 0, 0, 150, 20),
                                Box("continues here", 0, 24, 120, 20)
                            ]
                        }
                    ]
                }
            ]
        };

        var blocks = OcrLayoutAnalyzer.AnalyzeBlocks(result, LayoutAnalysisMode.Auto);

        Assert.Single(blocks);
        Assert.Equal(OcrLayoutSource.Provider, blocks[0].Source);
        Assert.Equal(1, blocks[0].Confidence);
        Assert.Equal("Provider first line continues here", blocks[0].Text);
        Assert.Equal(2, blocks[0].LineBoxPoints.Count);
    }

    [Fact]
    public void AutoFallsBackToSmartWithoutProviderLayout()
    {
        var result = new OcrResult
        {
            OcrContents =
            [
                Box("Smart first line", 0, 0, 140, 20),
                Box("continues here", 0, 24, 120, 20)
            ]
        };

        var blocks = OcrLayoutAnalyzer.AnalyzeBlocks(result, LayoutAnalysisMode.Auto);

        Assert.Single(blocks);
        Assert.Equal(OcrLayoutSource.Smart, blocks[0].Source);
        Assert.Equal("Smart first line continues here", blocks[0].Text);
    }

    [Fact]
    public void ProviderWithoutStructuredLayoutFallsBackToNoMerge()
    {
        var result = new OcrResult
        {
            OcrContents =
            [
                Box("One", 0, 0, 40, 20),
                Box("Two", 0, 24, 40, 20)
            ]
        };

        var blocks = OcrLayoutAnalyzer.AnalyzeBlocks(result, LayoutAnalysisMode.Provider);

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block => Assert.Equal(OcrLayoutSource.NoMerge, block.Source));
        Assert.Equal(["One", "Two"], blocks.Select(x => x.Text));
    }

    [Fact]
    public void ProviderFlatAnalyzeFallsBackToNoMerge()
    {
        var result = OcrLayoutAnalyzer.Analyze(
            [
                Box("One", 0, 0, 40, 20),
                Box("Two", 0, 24, 40, 20)
            ],
            LayoutAnalysisMode.Provider);

        Assert.Equal(["One", "Two"], result.Select(x => x.Text));
    }

    [Fact]
    public void OcrResultTextFlattensStructuredLayoutInReadingOrder()
    {
        var result = new OcrResult
        {
            Regions =
            [
                new()
                {
                    Paragraphs =
                    [
                        new()
                        {
                            Lines =
                            [
                                new() { Text = "First line" },
                                new() { Text = "continues" }
                            ]
                        },
                        new()
                        {
                            Lines =
                            [
                                new() { Text = "Second paragraph" }
                            ]
                        }
                    ]
                }
            ]
        };

        Assert.Equal($"First line continues{Environment.NewLine}Second paragraph", result.Text);
    }

    [Fact]
    public void PrepareOcrResultProjectsStructuredLayoutToFlatContents()
    {
        var result = new OcrResult
        {
            Regions =
            [
                new()
                {
                    Paragraphs =
                    [
                        new()
                        {
                            Lines =
                            [
                                Box("Projected first", 0, 0, 120, 20),
                                Box("line", 0, 24, 40, 20)
                            ]
                        }
                    ]
                }
            ]
        };

        Utilities.PrepareOcrResult(result);

        Assert.Single(result.OcrContents);
        Assert.Equal("Projected first line", result.OcrContents[0].Text);
        Assert.Equal(4, result.OcrContents[0].BoxPoints.Count);
    }

    [Fact]
    public void OcrPluginSupportBoxPointsControlsImageTranslationEligibility()
    {
        IOcrPlugin oldPlugin = new PlainOcrPlugin();
        IOcrPlugin eligible = new BoxPointOcrPlugin();

        Assert.False(oldPlugin.SupportBoxPoints());
        Assert.True(eligible.SupportBoxPoints());
    }

    private static List<OcrContent> AnalyzeSmart(params OcrContent[] contents) =>
        OcrLayoutAnalyzer.Analyze(contents, LayoutAnalysisMode.Smart);

    private static OcrContent Box(string text, float left, float top, float width, float height) =>
        new()
        {
            Text = text,
            BoxPoints =
            [
                new(left, top),
                new(left + width, top),
                new(left + width, top + height),
                new(left, top + height)
            ]
        };

    private class PlainOcrPlugin : IOcrPlugin
    {
        public IEnumerable<LangEnum> SupportedLanguages => [LangEnum.Auto];

        public void Init(IPluginContext context)
        {
        }

        public Control GetSettingUI() => new();

        public Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new OcrResult());

        public void Dispose()
        {
        }
    }

    private sealed class BoxPointOcrPlugin : IOcrPlugin
    {
        public IEnumerable<LangEnum> SupportedLanguages => [LangEnum.Auto];

        public bool SupportBoxPoints() => true;

        public void Init(IPluginContext context)
        {
        }

        public Control GetSettingUI() => new();

        public Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new OcrResult());

        public void Dispose()
        {
        }
    }
}
