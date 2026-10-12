"""demo_add 模块：演示技能链与服务链的示例模块。"""


def add(a, b):
    """两数相加。"""
    return a + b


def total_str(values):
    """将数值列表拼接为逗号分隔字符串。"""
    out = ""
    for v in values:
        out = out + str(v) + ","
    return out.rstrip(",")
