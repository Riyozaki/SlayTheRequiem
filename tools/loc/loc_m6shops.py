"""Тексты интерфейса лавки (M6 п. 5, docs/design/14 §1 и §4): вкладки «Купить / Продать / Услуги», список продажи,
шкала груза с превью, окно «Перегруз?» перед покупкой.

Ключи без префикса — build_loc.py добавляет «SLAYTHEREQUIEM-». SHOP_TIPS → static_hover_tips.json (Shops/ShopTabs.cs →
RequiemUi.TipText); SHOP_RELIC_EXTRA → relics.json (окно RequiemNet.Confirm у Книги торговца).
"""

SHOP_TIPS = {
    "eng": {
        "SHOP_TAB_BUY.title": "Buy",
        "SHOP_TAB_SELL.title": "Sell",
        "SHOP_TAB_SERVICES.title": "Services",
        "SHOP_SELL_HEADER.title": "Sell for {Percent}% of the price",
        "SHOP_SELL_HINT.title": "Only unequipped items from your pack. Equipped gear stays on you.",
        "SHOP_SELL_PRICE.title": "{Gold} gold",
        "SHOP_SELL_WEIGHT.title": "⚖ {Weight}",
        "SHOP_SELL_ONE.title": "Sell",
        "SHOP_SELL_ALL.title": "Sell all — {Gold} gold",
        "SHOP_SELL_MORE.title": "…and {Count} more (sell the ones above first or sell all)",
        "SHOP_SELL_EMPTY.title": "Nothing to sell: your pack is empty.",
        "SHOP_SELL_REAGENTS.title": "Reagents ×{Count}",
        "SHOP_SELL_BACK.title": "Back to the goods",
    },
    "rus": {
        "SHOP_TAB_BUY.title": "Купить",
        "SHOP_TAB_SELL.title": "Продать",
        "SHOP_TAB_SERVICES.title": "Услуги",
        "SHOP_SELL_HEADER.title": "Продажа за {Percent} % цены",
        "SHOP_SELL_HINT.title": "Только снятое снаряжение из рюкзака. Надетое остаётся на вас.",
        "SHOP_SELL_PRICE.title": "{Gold} зол.",
        "SHOP_SELL_WEIGHT.title": "⚖ {Weight}",
        "SHOP_SELL_ONE.title": "Продать",
        "SHOP_SELL_ALL.title": "Продать всё — {Gold} зол.",
        "SHOP_SELL_MORE.title": "…и ещё {Count} (сначала продайте верхние или всё разом)",
        "SHOP_SELL_EMPTY.title": "Продавать нечего: рюкзак пуст.",
        "SHOP_SELL_REAGENTS.title": "Реагенты ×{Count}",
        "SHOP_SELL_BACK.title": "К витрине",
    },
}

SHOP_RELIC_EXTRA = {
    "eng": {
        "MERCHANT_LEDGER.overloadHeader": "Overload?",
        "MERCHANT_LEDGER.overloadBody": "This weighs {Weight}: your Burden becomes {After} of {Capacity} — overload +{Excess}. "
                                        "Overload cuts ◆ regeneration, then your opening hand; above 10 you cannot pick anything up. Buy anyway?",
        "MERCHANT_LEDGER.overloadYes": "Buy",
        "MERCHANT_LEDGER.overloadNo": "Not now",
    },
    "rus": {
        "MERCHANT_LEDGER.overloadHeader": "Перегруз?",
        "MERCHANT_LEDGER.overloadBody": "Вес покупки — {Weight}: груз станет {After} из {Capacity}, перегруз +{Excess}. "
                                        "Перегруз снижает прирост ◆, затем стартовую руку; больше 10 — ничего не поднять. Всё равно купить?",
        "MERCHANT_LEDGER.overloadYes": "Купить",
        "MERCHANT_LEDGER.overloadNo": "Не сейчас",
    },
}
