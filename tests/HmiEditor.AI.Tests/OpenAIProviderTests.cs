using System.Net;
using System.Net.Http;
using HmiEditor.AI.Inference;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// OpenAIProvider 降级逻辑测试 —— 含 array 类型 JSON Schema 的中转服务兼容。
/// </summary>
public class OpenAIProviderTests
{
    [Fact]
    public void ContainsSchemaArray_WithArrayType_ReturnsTrue()
    {
        const string schema = """
            { "type": "object", "properties": {
              "controls": { "type": "array", "items": { "type": "object" } }
            } }
            """;
        Assert.True(OpenAIProvider.ContainsSchemaArray(schema));
    }

    [Fact]
    public void ContainsSchemaArray_NestedArray_ReturnsTrue()
    {
        const string schema = """
            { "type": "object", "properties": {
              "screen": { "type": "object", "properties": {
                "controls": { "type": "array", "items": { "type": "string" } }
              } }
            } }
            """;
        Assert.True(OpenAIProvider.ContainsSchemaArray(schema));
    }

    [Fact]
    public void ContainsSchemaArray_NoArray_ReturnsFalse()
    {
        const string schema = """
            { "type": "object", "properties": {
              "name": { "type": "string" },
              "width": { "type": "number" }
            } }
            """;
        Assert.False(OpenAIProvider.ContainsSchemaArray(schema));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    public void ContainsSchemaArray_InvalidInput_ReturnsFalse(string? schema)
    {
        Assert.False(OpenAIProvider.ContainsSchemaArray(schema));
    }

    [Fact]
    public void ContainsSchemaArray_ArrayInPropertiesValues_Detects()
    {
        // 真实模板 schema: parameters 数组
        const string schema = """
            { "type": "object", "properties": {
              "template": { "type": "object", "properties": {
                "name": { "type": "string" },
                "controls": { "type": "array" },
                "parameters": { "type": "array" }
              } }
            } }
            """;
        Assert.True(OpenAIProvider.ContainsSchemaArray(schema));
    }

    // ── 三层降级行为验证 ──

    private const string TestSchema =
        """{"type":"object","properties":{"screen":{"type":"object","properties":{"controls":{"type":"array"}}}}}""";

    private static LlmRequest BuildRequest() => new()
    {
        Messages = new List<LlmMessage> { new() { Role = LlmRoles.User, Content = "hi" } },
        JsonSchema = TestSchema
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_responder(request));
    }

    [Fact]
    public async Task ChatAsync_JsonSchemaRejected400_DegradesToJsonObjectAndSucceeds()
    {
        var requests = 0;
        var handler = new StubHandler(_ =>
        {
            requests++;
            if (requests == 1)
            {
                // 中转服务拒绝 strict json_schema → 400
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":{\"message\":\"strict schema not supported\"}}")
                };
            }
            // 降级 json_object 成功
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"{\"screen\":{\"name\":\"降级成功\"}}"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""")
            };
        });

        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "test", Model = "m" }, handler);

        var result = await provider.ChatAsync(BuildRequest());

        Assert.Equal(2, requests);          // json_schema 失败后降级 json_object 重试
        Assert.Contains("降级成功", result.Content);
    }

    [Fact]
    public async Task ChatAsync_JsonSchemaRejected500_DegradesToJsonObjectAndSucceeds()
    {
        // 服务端对 strict json_schema 返回 500(部分中转把不支持的 response_format 当服务端错误) → 应降级
        var requests = 0;
        var handler = new StubHandler(_ =>
        {
            requests++;
            if (requests == 1)
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                { Content = new StringContent("{\"error\":\"strict schema crashed proxy\"}") };
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("""{"choices":[{"message":{"content":"{\"screen\":{\"name\":\"500降级成功\"}}"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""") };
        });

        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "test", Model = "m", MaxRetries = 0 }, handler);

        var result = await provider.ChatAsync(BuildRequest());

        Assert.Contains("500降级成功", result.Content);
    }

    [Fact]
    public async Task ChatAsync_AllFormatsRejected400_ThrowsLlmProviderException()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"bad request\"}")
            });

        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "test", Model = "m" }, handler);

        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => provider.ChatAsync(BuildRequest()));
        Assert.Contains("均被拒绝", ex.Message);
    }

    [Fact]
    public async Task ChatAsync_EmptySchema_SendsWithoutResponseFormatAndSucceeds()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"{\"screen\":{\"name\":\"无约束\"}}"}}],"usage":{"prompt_tokens":1,"completion_tokens":1}}""")
            });

        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "test", Model = "m" }, handler);

        var request = new LlmRequest
        {
            Messages = new List<LlmMessage> { new() { Role = LlmRoles.User, Content = "hi" } },
            JsonSchema = "  " // 空白 schema 不应导致解析异常
        };

        var result = await provider.ChatAsync(request);

        Assert.Contains("无约束", result.Content);
    }
}
