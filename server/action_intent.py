NONE = "none"
TAUNT = "taunt"
ALLOWED_ACTIONS = frozenset({NONE, TAUNT})


_TAUNT_INTENTS = frozenset(
    {
        "森森给我表演一下",
        "给我表演一下",
        "给我表演一个",
        "做个动作",
        "来一个动作",
        "给我看看taunt",
        "森森逗我一下",
        "逗我一下",
        "showmeataunt",
        "performataunt",
        "给我表演一下再告诉我你吃什么",
    }
)


def resolve_action_suggestion(message: str) -> str:
    normalized = "".join(
        character.casefold()
        for character in str(message or "")
        if character.isalnum()
    )
    return TAUNT if normalized in _TAUNT_INTENTS else NONE
