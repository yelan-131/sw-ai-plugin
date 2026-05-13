import json
from pathlib import Path

CONFIG_PATH = Path(__file__).parent / "config.json"

def load_config() -> dict:
    if CONFIG_PATH.exists():
        try:
            with open(CONFIG_PATH, encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return {}

def save_config(config: dict):
    existing = load_config()
    existing.update(config)
    with open(CONFIG_PATH, "w", encoding="utf-8") as f:
        json.dump(existing, f, indent=2, ensure_ascii=False)

def get_api_key() -> str:
    import os
    key = os.environ.get("ANTHROPIC_API_KEY", "")
    if key:
        return key
    return load_config().get("anthropic_api_key", "")
