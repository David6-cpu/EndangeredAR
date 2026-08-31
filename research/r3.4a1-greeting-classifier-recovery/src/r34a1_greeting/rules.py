from __future__ import annotations

import re
import unicodedata


BLOCKED_FRAGMENTS = (
    "是什么意思",
    "什么含义",
    "怎么解释",
    "怎么写",
    "请解释",
    "为什么",
    "不要",
    "不用",
    "没有",
    "不是",
    "别问好",
    "别挥手",
    "不许",
    "他说",
    "她说",
    "它说",
    "他们说",
    "引用",
    "转述",
    "剧本",
    "台词",
    "字典",
    "朗读",
    "翻译",
    "反义",
    "例子",
    "句型",
    "句式",
    "疑问句",
    "礼貌用语",
    "分类成",
    "标签",
    "输出greeting",
    "忽略规则",
    "忽略分类",
    "prompt",
    "animator",
    "settrigger",
    "wave",
    "你好像",
    "你好不好",
    "科学事实",
    "学名",
    "食物任务",
    "当前进度",
    "currentprogress",
    "charactermemory",
    "historyboundary",
    "system_status",
    "systemstatus",
    "你还记得",
)


DIRECT_PATTERNS = (
    r"^(?:森森[,，:：]?)?(?:你好(?:呀|啊|哇)?|您好(?:呀)?|嗨+|嗨呀|哈喽(?:呀)?|哈啰|hello|hi)(?:[,， ]?(?:小?森森))?(?:[,，](?:第一次来见你|我是第一次来|终于见到你|很高兴见到你|很高兴认识你|我来看你啦|我来啦|我们又碰面了))?[!！。,.，]*$",
    r"^(?:森森[,，:：]?)?(?:早上好|上午好|中午好|下午好|傍晚好|晚上好|夜晚好|午后好|早安|午安|晚安)(?:呀|啊)?(?:[,， ]?(?:小?森森))?(?:[,，](?:新的一天见面啦|我又来找你了|今天也见到你了))?[!！。,.，]*$",
    r"^(?:森森[,，:：]?)?(?:好久不见|又见面了|很高兴见到你|很高兴又见到你|我又来看你了|我来看你啦|欢迎回来)[!！。,.，]*$",
    r"^(?:森森[,，:：]?)?(?:你好吗|最近好吗|最近过得好吗)[?？!！。]*$",
)

REUNION_FRAGMENTS = (
    "好久没来看你",
    "好久没有见",
    "又来和你见面",
    "又来看你",
    "又来陪你",
    "又来找你",
    "再次见到你",
    "终于又见到你",
    "终于见到你",
    "久别重逢",
    "久违了",
    "我又回来",
    "我回来",
    "重新见到你",
    "很开心能再次见到你",
    "初次见面",
    "来向你问好",
)


def normalize_message(value: str) -> str:
    value = unicodedata.normalize("NFKC", value).strip().lower()
    value = re.sub(r"\s+", "", value)
    return value


def deterministic_greeting_intent(user_message: str) -> bool:
    text = normalize_message(user_message)
    if not text or len(text) > 32:
        return False
    if any(fragment in text for fragment in BLOCKED_FRAGMENTS):
        return False
    if any(mark in text for mark in ('"', "'", "“", "”", "‘", "’", "《", "》")):
        return False
    if any(re.fullmatch(pattern, text, flags=re.IGNORECASE) for pattern in DIRECT_PATTERNS):
        return True
    return any(fragment in text for fragment in REUNION_FRAGMENTS)
