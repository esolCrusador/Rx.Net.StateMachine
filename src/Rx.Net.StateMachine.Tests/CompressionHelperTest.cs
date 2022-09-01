using FluentAssertions;
using Rx.Net.StateMachine.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    public class CompressionHelperTest
    {
        [Theory]
        [InlineData("Hello")]
        [InlineData("World")]
        public void ShouldCompressAndDecompress(string input)
        {
            var inputBytes = Encoding.UTF8.GetBytes(input);
            using var stream = new MemoryStream(inputBytes);

            string zipped = CompressionHelper.Zip(stream);
            var result = CompressionHelper.Unzip(zipped);

            string output = Encoding.UTF8.GetString(result.ToArray());
            input.Should().Be(output);
        }
    }
}
