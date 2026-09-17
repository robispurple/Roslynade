using System.Text.Json;
using Roslynade.Models;
using Spectre.Console;

namespace Roslynade.Tests
{
    public class FoundryModelTests
    {
        [Fact]
        public void Deserialize_FoundryModelCatalogJson_ParsesDownloadedAndUndownloadedModels()
        {
            string json = """
            {
              "models": [
                {
                  "alias": "qwen2.5-coder-14b",
                  "id": "qwen2.5-coder-14b-instruct-generic-gpu:4",
                  "displayName": "qwen2.5-coder-14b-instruct-generic-gpu",
                  "type": "Chat",
                  "device": "Gpu",
                  "fileSizeMb": 9000,
                  "cached": true,
                  "license": "apache-2.0",
                  "supportsToolCalling": true
                },
                {
                  "alias": "phi-4-mini",
                  "id": "Phi-4-mini-instruct-generic-gpu:5",
                  "displayName": "Phi-4-mini-instruct-generic-gpu",
                  "type": "Chat",
                  "device": "Gpu",
                  "fileSizeMb": 3809,
                  "cached": false,
                  "license": "MIT",
                  "supportsToolCalling": true
                }
              ]
            }
            """;

            var catalog = JsonSerializer.Deserialize<FoundryModelCatalogResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(catalog);
            Assert.Equal(2, catalog.Models.Count);

            var downloaded = catalog.Models.Where(m => m.Cached).ToList();
            var undownloaded = catalog.Models.Where(m => !m.Cached).ToList();

            Assert.Single(downloaded);
            Assert.Equal("qwen2.5-coder-14b", downloaded[0].Alias);
            Assert.True(downloaded[0].Cached);
            Assert.Equal("8.8 GB", downloaded[0].FormattedSize);

            Assert.Single(undownloaded);
            Assert.Equal("phi-4-mini", undownloaded[0].Alias);
            Assert.False(undownloaded[0].Cached);
            Assert.Equal("3.7 GB", undownloaded[0].FormattedSize);
        }

        [Fact]
        public void FormattedSize_Under1024Mb_RendersMegabytes()
        {
            var model = new FoundryModelInfo
            {
                Alias = "tiny-model",
                FileSizeMb = 528,
                Cached = false
            };

            Assert.Equal("528 MB", model.FormattedSize);
        }

        [Fact]
        public void FormattedSize_Over1024Mb_RendersGigabytes()
        {
            var model = new FoundryModelInfo
            {
                Alias = "large-model",
                FileSizeMb = 10516,
                Cached = true
            };

            Assert.Equal("10.3 GB", model.FormattedSize);
        }

        [Fact]
        public void ModelChoices_Markup_ParsesWithoutExceptions()
        {
            var downloadedModel = new FoundryModelInfo
            {
                Alias = "qwen2.5-coder-14b",
                Device = "Gpu",
                FileSizeMb = 9000,
                Type = "Chat",
                Cached = true
            };

            var undownloadedModel = new FoundryModelInfo
            {
                Alias = "phi-4-mini",
                Device = "Gpu",
                FileSizeMb = 3809,
                Type = "Chat",
                Cached = false
            };

            string downloadedMarkup = $"[green]● [[Downloaded]][/]      [bold white]{Markup.Escape(downloadedModel.Alias)}[/] [grey]({downloadedModel.Device}, {downloadedModel.FormattedSize}, {downloadedModel.Type})[/]";
            string undownloadedMarkup = $"[yellow]○ [[Not Downloaded]][/]  [white]{Markup.Escape(undownloadedModel.Alias)}[/] [grey]({undownloadedModel.Device}, {undownloadedModel.FormattedSize}, {undownloadedModel.Type})[/]";
            string refreshMarkup = "[cyan]↻ [[Refresh / Re-poll models]][/]";
            string customMarkup = "[grey]✍ [[Enter custom model alias manually]][/]";

            // Should parse without InvalidOperationException
            var m1 = new Markup(downloadedMarkup);
            var m2 = new Markup(undownloadedMarkup);
            var m3 = new Markup(refreshMarkup);
            var m4 = new Markup(customMarkup);

            Assert.NotNull(m1);
            Assert.NotNull(m2);
            Assert.NotNull(m3);
            Assert.NotNull(m4);
        }
    }
}
