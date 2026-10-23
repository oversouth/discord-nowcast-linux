from setuptools import setup, find_packages

setup(
    name="discord-nowcast",
    version="2.0.0",
    description="Show playing track in Discord status for Linux & NixOS",
    packages=find_packages(),
    entry_points={
        "console_scripts": [
            "discord-nowcast=nowcast.cli:main",
        ],
    },
    python_requires=">=3.8",
)
