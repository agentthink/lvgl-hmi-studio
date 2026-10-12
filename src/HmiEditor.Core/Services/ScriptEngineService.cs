using HmiEditor.Core.Models.Scripting;

namespace HmiEditor.Core.Services;

/// <summary>
/// 脚本引擎服务 —— 管理脚本的 CRUD、模板提供、事件绑定、语法校验。
///
/// <para><b>编辑器侧不做 Lua 执行</b>：执行走 <see cref="ScriptSandboxExecutor"/> 的自研简化子集
/// （只依赖 <c>ExpressionEngine</c>，零外部依赖）；脚本代码本身嵌入生成的 C 代码，
/// 由固件通过 LVGL 的 <c>lv_lua_binding</c> 在运行时执行。</para>
///
/// <para><b>校验分两层</b>：语法错走 <see cref="LuaSyntaxValidator"/>（MoonSharp，<b>只解析不执行</b>），
/// 引入真解析器只为报出带行号的语法错，不带来任何执行能力；另有**运行前检查**
/// <see cref="LuaDialect.CheckRuntimeSymbols"/> —— 扫出"语法合法、但设备上可能没这个库 / 字段"
/// 的引用（<c>bit32</c> / <c>table.pack</c> / <c>utf8</c> …），这类问题真解析在原理上看不见。
/// 后者只产出**警告**，不让脚本变红，理由见 <see cref="ScriptValidationResult.Warnings"/>。</para>
/// </summary>
public class ScriptEngineService : IScriptEngine
{
    private readonly Dictionary<string, HmiScript> _scripts = new();
    private readonly List<ScriptBinding> _bindings = new();
    private readonly object _lock = new();

    public bool IsInitialized { get; private set; }

    public event EventHandler<HmiScript>? ScriptLoaded;
    public event EventHandler<ScriptBinding>? BindingChanged;

    public Action<string>? Log { get; set; }

    // ── 脚本管理 ──

    public void LoadScript(HmiScript script)
    {
        ArgumentNullException.ThrowIfNull(script);
        lock (_lock)
        {
            _scripts[script.Id] = script;
        }
        ScriptLoaded?.Invoke(this, script);
        Log?.Invoke($"[ScriptEngine] 加载脚本: {script.Name} ({script.Category})");
    }

    public void UnloadScript(string scriptId)
    {
        lock (_lock)
        {
            _scripts.Remove(scriptId);
            _bindings.RemoveAll(b => b.ScriptId == scriptId);
        }
        Log?.Invoke($"[ScriptEngine] 卸载脚本: {scriptId}");
    }

    public IReadOnlyList<HmiScript> GetLoadedScripts()
    {
        lock (_lock)
        {
            return _scripts.Values.ToList();
        }
    }

    public HmiScript? GetScript(string scriptId)
    {
        lock (_lock)
        {
            return _scripts.GetValueOrDefault(scriptId);
        }
    }

    /// <summary>
    /// 获取指定类别的脚本列表。
    /// </summary>
    public IReadOnlyList<HmiScript> GetScriptsByCategory(string category)
    {
        lock (_lock)
        {
            return _scripts.Values.Where(s => s.Category == category).ToList();
        }
    }

    // ── 语法校验 ──

    public ScriptValidationResult Validate(HmiScript script)
    {
        var result = new ScriptValidationResult();

        if (string.IsNullOrWhiteSpace(script.Name))
        {
            result.IsValid = false;
            result.Errors.Add(new ScriptError { Code = "EMPTY_NAME", Message = "脚本名称不能为空" });
        }

        if (string.IsNullOrWhiteSpace(script.Code))
        {
            result.IsValid = false;
            result.Errors.Add(new ScriptError { Code = "EMPTY_CODE", Message = "脚本内容不能为空" });
        }
        else
        {
            // ① 真语法解析（MoonSharp，只 LoadString 不执行）—— 错误带行号。
            //    放最前：最具体，而 UI 只展示 Errors[0]。
            var syntax = LuaSyntaxValidator.Validate(script.Code);
            if (!syntax.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(syntax.Errors);
            }

            // ② 模型级约束：必须有函数定义。运行时 dispatch_* 靠 lua_getglobal(FunctionName)
            //    按名查找，没有函数体的脚本在设备上必然静默失败 ⇒ 这是**契约检查**，不是风格建议。
            //    判据用「剥注释后查关键字」而非裸 Contains("function ") —— 后者会被注释里的
            //    示例（-- function on_click()）骗过，让本该报错的脚本悄悄通过。
            if (!LuaSyntaxValidator.HasFunctionDefinition(script.Code))
            {
                result.IsValid = false;
                result.Errors.Add(new ScriptError
                {
                    Code = "NO_FUNCTION",
                    Message = "脚本必须包含至少一个函数定义",
                    Line = 1,
                });
            }

            // ③ 运行前检查（**独立机制**，不是方言对齐的一部分）：语法合法、却引用了设备上
            //    可能不存在的库 / 字段（bit32 / table.pack / math.type / utf8 …）。
            //    这类问题真解析**原理上看不见**（LuaRejectStage.Runtime）—— 无论把解析器
            //    换成哪个版本都看不见，只能单独扫。
            //
            //    走**警告**通道，不写进 Errors：设备侧目标版本至今未定（LuaDialect.Target
            //    = Unknown），同一个 bit32 在 5.2 上完全正常、在 5.1 上必炸。判红就等于把
            //    "待定"当成"错误"，用户会看到满屏红色、却在设备上跑得起来的脚本。
            //
            //    只在前两关都过时做：语法都没解析成功时，扫出来的符号引用对用户是噪声，
            //    他此刻该看的是上面那条语法错。
            if (syntax.IsValid)
            {
                foreach (var hit in LuaDialect.CheckRuntimeSymbols(script.Code, LuaDialect.Target))
                {
                    // Code/Args（界面查字典用）与 Message（字典缺键时的回落）**同源**：
                    // 都出自这一次 hit 的判定。两态各自一个 Code —— 话术方向相反，
                    // 合用一个就只能把区分塞进 Args，而 Args 是填模板用的、不是分派用的。
                    var (code, args) = LuaDialect.DescribeHit(hit, LuaDialect.Target);

                    result.Warnings.Add(new ScriptError
                    {
                        Line = hit.Line,
                        Column = hit.Column,
                        Code = code,
                        Message = LuaDialect.ExplainHit(hit, LuaDialect.Target),
                        Args = args,
                    });
                }
            }
        }

        return result;
    }

    // ── 事件绑定管理 ──

    /// <summary>
    /// 添加控件事件绑定。
    /// </summary>
    public void AddBinding(ScriptBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        lock (_lock)
        {
            _bindings.Add(binding);
        }
        BindingChanged?.Invoke(this, binding);
        Log?.Invoke($"[ScriptEngine] 添加绑定: {binding.ControlId}.{binding.EventName} → {binding.FunctionName}");
    }

    /// <summary>
    /// 移除控件事件绑定。
    /// </summary>
    public void RemoveBinding(string bindingId)
    {
        lock (_lock)
        {
            _bindings.RemoveAll(b => b.Id == bindingId);
        }
    }

    /// <summary>
    /// 获取指定控件的所有绑定。
    /// </summary>
    public IReadOnlyList<ScriptBinding> GetBindingsForControl(string controlId)
    {
        lock (_lock)
        {
            return _bindings.Where(b => b.ControlId == controlId).ToList();
        }
    }

    /// <summary>
    /// 获取所有事件绑定。
    /// </summary>
    public IReadOnlyList<ScriptBinding> GetAllBindings()
    {
        lock (_lock)
        {
            return _bindings.ToList();
        }
    }

    // ── 模板提供 ──

    /// <summary>
    /// 获取可用的预置模板列表。
    /// </summary>
    public static IReadOnlyList<ScriptTemplate> GetTemplates() => new[]
    {
        ScriptTemplate.OnClick,
        ScriptTemplate.OnValueChanged,
        ScriptTemplate.Timer,
        ScriptTemplate.Alarm,
    };

    /// <summary>
    /// 根据模板创建新脚本。
    /// </summary>
    public HmiScript CreateFromTemplate(ScriptTemplate template)
    {
        return new HmiScript
        {
            Name = $"{template.Name} - {DateTime.Now:HHmmss}",
            Category = template.Category,
            Code = template.Code,
            Description = template.Description,
        };
    }

    // ── 初始化/清理 ──

    public void Initialize()
    {
        IsInitialized = true;
        Log?.Invoke("[ScriptEngine] 已初始化");
    }

    public void Shutdown()
    {
        lock (_lock)
        {
            _scripts.Clear();
            _bindings.Clear();
        }
        IsInitialized = false;
        Log?.Invoke("[ScriptEngine] 已关闭");
    }
}
