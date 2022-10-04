using FluentAssertions;
using Rx.Net.StateMachine.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Rx.Net.StateMachine.Tests
{
    public class CompressionHelperTest
    {
        [Theory]
        [InlineData("{ Result: \"qqqqqqqqqqqqwewreewrwerewrwerwerew\", Steps: {\"Step1\": 123456, \"Step2\": 4543543543, \"Prefffffixxxxx.Step5\": \"qweqweqwwtrgfbfgtytrytrytrytrrwerew\", \"Prefffffixxxxx.Step6\": \"retertretertretertre\"}, \"Items\": {\"Item1\": \"wqewqerwrwgdfbvcbfhtrytyjhmnvcxdfeqqqsdadasgfdgertretgdfgfdgfdg\", \"RRRRRR.Item2\": \"32423essfdsfgdfghfghretertergdffgnvb vb swrwerewrwe\", \"eqwdsfstehfncsaeqweqeqtfsgfdgdf4\": \"weqw12fsdgtrrgfngfnfytrhnfhtw1qweqweqdsgertertre\" }}")]
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
