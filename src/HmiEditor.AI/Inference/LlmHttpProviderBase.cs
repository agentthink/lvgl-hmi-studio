using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HmiEditor.Core.Serialization;

namespace HmiEditor.AI.Inference;

/// <summary>
/// LLM HTTP Provider 基类 —— 统一处理超时、重试、错误映射与 JSON 序列化。
/// 各 Provider 只负责翻译 LlmRequest/LlmResponse 到自身 API 协议。
/// </summary>
public abstract class LlmHttpProviderBase : ILlmProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly LlmProviderConfig _config;

    protected LlmHttpProviderBase(LlmProviderConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds) };
    }

    /// <summary>供测试注入自定义 HttpMessageHandler(模拟 API 响应/错误)。</summary>
    protected LlmHttpProviderBase(LlmProviderConfig config, HttpMessageHandler handler)
    {
        _config = config;
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds) };
    }

    public abstract string ProviderName { get; }

    /// <summary>子类实现具体 API 协议翻译。</summary>
    public abstract Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default);

    protected LlmProviderConfig Config => _config;

    /// <summary>发送请求并自动重试。仅对可重试错误 (网络错误 / 429 / 5xx) 重试。每次重试都会新建 HttpRequestMessage。429 会读取 Retry-After 头并延长退避。</summary>
    protected async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken ct)
    {
        Exception? lastError = null;
        System.Net.HttpStatusCode? lastStatus = null;
        TimeSpan? lastRetryAfter = null;
        var retries = Math.Max(0, _config.MaxRetries);

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            if (attempt > 0)
            {
                var backoff = ComputeBackoff(attempt, lastStatus, lastRetryAfter);
                await Task.Delay(backoff, ct);
                lastRetryAfter = null; // 消费后清空
            }

            using var request = requestFactory();
            try
            {
                var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.IsSuccessStatusCode)
                {
                    // 成功路径: 响应对象所有权转移给调用方, 由调用方释放
                    return response;
                }

                lastStatus = response.StatusCode;

                // 429 与 5xx 可重试; 4xx (参数错误/鉴权失败) 直接失败
                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                {
                    // 尝试读取 Retry-After 头(秒数), 让下一轮循环使用它作为退避
                    if (response.Headers.RetryAfter?.Delta is { } retryAfter)
                    {
                        lastRetryAfter = retryAfter + TimeSpan.FromSeconds(1); // 加 1s 缓冲
                        response.Dispose();
                        lastError = new LlmProviderException(
                            $"LLM API 返回 429 Too Many Requests (Retry-After: {retryAfter.TotalSeconds:F0}s)",
                            response.StatusCode, retryAfterSeconds: (int)lastRetryAfter.Value.TotalSeconds);
                        continue;
                    }

                    response.Dispose();
                    lastError = new LlmProviderException(
                        $"LLM API 返回 {(int)response.StatusCode} {response.ReasonPhrase}",
                        response.StatusCode);
                    continue;
                }

                // 不可重试错误: 读取错误体以提供诊断信息
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                response.Dispose();
                throw new LlmProviderException(
                    $"LLM API 返回 {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(errorBody, 500)}",
                    response.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                lastStatus = null;
                lastError = new LlmProviderException($"LLM API 网络错误: {ex.Message}", null, ex);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                lastStatus = null;
                lastError = new LlmProviderException("LLM API 请求超时");
            }
        }

        var is429 = lastStatus == System.Net.HttpStatusCode.TooManyRequests;
        var friendly = is429
            ? $"API 限流 (429)，建议稍后重试或简化请求。如频繁触发，请检查并发请求或降低系统提示词长度。"
            : $"LLM API 请求失败 (重试 {retries} 次后仍失败)";
        var retryAfterSec = (lastError as LlmProviderException)?.RetryAfterSeconds;

        // 把最后一次的真实原因拼进去。否则 "重试 N 次后仍失败" 会把根因（超时？500？DNS 失败？）
        // 整个吞掉 —— 用户与日志都只剩一句泛化文案，没法判断该改配置还是该等一会儿。
        var reason = (lastError as LlmProviderException)?.Message;
        if (!string.IsNullOrWhiteSpace(reason) && !friendly.Contains(reason, StringComparison.Ordinal))
            friendly = $"{friendly}: {reason}";

        throw new LlmProviderException(friendly, lastStatus, lastError, retryAfterSec);
    }

    /// <summary>
    /// 是否属于「请求形态」类错误（参数 400/422 或服务端 5xx）—— 换一种请求形态重试有意义。
    /// 刻意不含 429（限流，换形态无用）与网络错误（无状态码，重试同样形态即可，由
    /// <see cref="SendWithRetryAsync"/> 负责），也不含 401/403（鉴权，换形态必然再失败）。
    /// </summary>
    protected static bool IsRequestShapeError(LlmProviderException ex) =>
        ex.StatusCode is { } sc && ((int)sc == 400 || (int)sc == 422 || (int)sc >= 500);

    /// <summary>计算退避时间: 429 优先用 Retry-After，否则 5/10/20s；5xx 与网络错误 1/2/4s。</summary>
    private static TimeSpan ComputeBackoff(int attempt, System.Net.HttpStatusCode? lastStatus, TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue)
            return retryAfter.Value;

        var isRateLimited = lastStatus == System.Net.HttpStatusCode.TooManyRequests;
        var seconds = isRateLimited
            ? 5 * Math.Pow(2, attempt - 1)
            : Math.Pow(2, attempt - 1);
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>将 LlmRequest 序列化为 JSON,注入 JSON Schema 约束(子类覆盖选择注入方式)。</summary>
    protected static string SerializeBody(object body) =>
        JsonSerializer.Serialize(body, JsonOptions);

    protected static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions);

    /// <summary>通用 JSON 选项: 大小写不敏感 + 宽松(容错未知字段)。</summary>
    protected static readonly JsonSerializerOptions JsonOptions = JsonOptionsDefaults.Llm;

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// LLM Provider 调用异常。
/// </summary>
public sealed class LlmProviderException : Exception
{
    public System.Net.HttpStatusCode? StatusCode { get; }

    /// <summary>服务端建议的等待秒数(429 Retry-After)。</summary>
    public int? RetryAfterSeconds { get; }

    public LlmProviderException(string message, System.Net.HttpStatusCode? statusCode = null, Exception? inner = null, int? retryAfterSeconds = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        RetryAfterSeconds = retryAfterSeconds;
    }
}
