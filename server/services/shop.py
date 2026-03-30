"""
礼物商店 — 推荐 / 每日刷新 / 神秘盒子
"""

import json
import random
from datetime import date
from pathlib import Path

GIFT_CONFIG_PATH = Path(__file__).parent.parent / "data" / "gift_config.json"
_gift_config = None
_daily_cache = {"date": "", "items": []}


def _load_gifts():
    global _gift_config
    if _gift_config is None:
        with open(GIFT_CONFIG_PATH, "r", encoding="utf-8") as f:
            _gift_config = json.load(f)
    return _gift_config


def get_shop(preferences: dict) -> dict:
    """获取商店数据：推荐 + 每日精选 + 全部"""
    gifts = _load_gifts()

    # 推荐：按喜好度排序
    recommended = _get_recommended(gifts, preferences)

    # 每日精选：每天随机3个
    daily = _get_daily(gifts)

    # 全部按分类
    categories = {}
    for gid, g in gifts.items():
        cat = g.get("category", "其他")
        if cat not in categories:
            categories[cat] = []
        categories[cat].append({
            "id": gid,
            "name": g["name"],
            "cost": g["cost"],
            "rarity": g["rarity"],
            "base_favor": g["base_favor"],
        })

    return {
        "recommended": recommended,
        "daily_special": daily,
        "categories": categories,
        "mystery_box_cost": 30,
    }


def _get_recommended(gifts: dict, preferences: dict) -> list:
    """根据 AI 喜好推荐礼物"""
    scored = []
    for gid, g in gifts.items():
        tags = g.get("tags", [])
        like = max((preferences.get(t, 0) for t in tags), default=0)
        scored.append((like, gid, g))

    scored.sort(reverse=True)
    result = []
    for like, gid, g in scored[:5]:
        hint = ""
        if like > 0.7:
            hint = "她超喜欢这个！"
        elif like > 0.3:
            hint = "她应该会挺开心"
        elif like > -0.3:
            hint = "她觉得还行"
        else:
            hint = "她可能不太喜欢..."

        result.append({
            "id": gid,
            "name": g["name"],
            "cost": g["cost"],
            "rarity": g["rarity"],
            "hint": hint,
        })

    return result


def _get_daily(gifts: dict) -> list:
    """每日精选（每天固定随机3个）"""
    today = date.today().isoformat()
    if _daily_cache["date"] == today:
        return _daily_cache["items"]

    all_ids = list(gifts.keys())
    random.seed(today)
    daily_ids = random.sample(all_ids, min(3, len(all_ids)))

    items = []
    for gid in daily_ids:
        g = gifts[gid]
        items.append({
            "id": gid,
            "name": g["name"],
            "cost": max(1, int(g["cost"] * 0.7)),  # 每日精选 7 折
            "rarity": g["rarity"],
            "original_cost": g["cost"],
            "discount": "7折",
        })

    _daily_cache["date"] = today
    _daily_cache["items"] = items
    return items


def open_mystery_box(gifts: dict) -> dict:
    """开启神秘盒子 — 随机获得一个礼物"""
    all_items = list(gifts.items())

    # 按稀有度加权
    weights = []
    for gid, g in all_items:
        r = g.get("rarity", "common")
        w = {"common": 50, "rare": 30, "epic": 15, "legendary": 5}.get(r, 50)
        weights.append(w)

    chosen_id, chosen = random.choices(all_items, weights=weights, k=1)[0]
    return {
        "gift_id": chosen_id,
        "gift_name": chosen["name"],
        "rarity": chosen["rarity"],
    }
