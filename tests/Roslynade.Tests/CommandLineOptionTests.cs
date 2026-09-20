using Roslynade;
using Xunit;

namespace Roslynade.Tests
{
    public class CommandLineOptionTests
    {
        [Fact]
        public void ResolveConcurrency_WhenExplicitlySet_ReturnsSetValue()
        {
            var options = new CommandLineOptions
            {
                ExplicitConcurrency = 4
            };

            Assert.Equal(4, options.ResolveConcurrency());
        }

        [Fact]
        public void ResolveConcurrency_WhenExplicitlyZeroOrNegative_ClampsToOne()
        {
            var optionsZero = new CommandLineOptions { ExplicitConcurrency = 0 };
            var optionsNegative = new CommandLineOptions { ExplicitConcurrency = -3 };

            Assert.Equal(1, optionsZero.ResolveConcurrency());
            Assert.Equal(1, optionsNegative.ResolveConcurrency());
        }

        [Fact]
        public void ResolveConcurrency_WhenEndpointSet_DefaultsToThree()
        {
            var options = new CommandLineOptions
            {
                ExplicitEndpoint = "https://models.inference.ai.azure.com"
            };

            Assert.Equal(3, options.ResolveConcurrency());
        }

        [Fact]
        public void ResolveConcurrency_WhenNoEndpoint_DefaultsToOne()
        {
            var options = new CommandLineOptions();

            Assert.Equal(1, options.ResolveConcurrency());
        }

        [Fact]
        public void Parse_ConcurrencyLongFlag_SetsConcurrency()
        {
            var options = CommandLineOptions.Parse(new[] { "--concurrency", "5" });
            Assert.Equal(5, options.ExplicitConcurrency);
            Assert.Equal(5, options.ResolveConcurrency());
        }

        [Fact]
        public void Parse_ConcurrencyLongFlagWithEquals_SetsConcurrency()
        {
            var options = CommandLineOptions.Parse(new[] { "--concurrency=6" });
            Assert.Equal(6, options.ExplicitConcurrency);
            Assert.Equal(6, options.ResolveConcurrency());
        }

        [Fact]
        public void Parse_ConcurrencyShortFlag_SetsConcurrency()
        {
            var options = CommandLineOptions.Parse(new[] { "-c", "2" });
            Assert.Equal(2, options.ExplicitConcurrency);
            Assert.Equal(2, options.ResolveConcurrency());
        }

        [Fact]
        public void Parse_ConcurrencyShortFlagWithEquals_SetsConcurrency()
        {
            var options = CommandLineOptions.Parse(new[] { "-c=4" });
            Assert.Equal(4, options.ExplicitConcurrency);
            Assert.Equal(4, options.ResolveConcurrency());
        }

        [Fact]
        public void Parse_EndpointWithoutConcurrency_ResolvesToThree()
        {
            var options = CommandLineOptions.Parse(new[] { "--endpoint", "http://localhost:11434/v1" });
            Assert.Null(options.ExplicitConcurrency);
            Assert.Equal(3, options.ResolveConcurrency());
        }

        [Fact]
        public void Parse_EndpointWithExplicitConcurrency_OverridesDefault()
        {
            var options = CommandLineOptions.Parse(new[] { "--endpoint", "http://localhost:11434/v1", "-c", "7" });
            Assert.Equal(7, options.ExplicitConcurrency);
            Assert.Equal(7, options.ResolveConcurrency());
        }

        [Fact]
        public void Parse_ModelAndDeviceFlags_ParsedCorrectly()
        {
            var options = CommandLineOptions.Parse(new[] { "--model", "qwen2.5-coder-14b", "--gpu" });
            Assert.Equal("qwen2.5-coder-14b", options.ExplicitModel);
            Assert.Equal("GPU", options.ExplicitDevice);
            Assert.Equal(1, options.ResolveConcurrency());
        }
    }
}

