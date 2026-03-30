from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    llm_base_url: str = "https://api.deepseek.com/v1"
    llm_api_key: str = "your_api_key_here"
    llm_model: str = "deepseek-chat"
    database_url: str = "sqlite:///./symbiosis.db"

    voice_app_id: str = ""
    voice_access_token: str = ""
    voice_speaker: str = "zh_female_cancan"

    class Config:
        env_file = ".env"


settings = Settings()
