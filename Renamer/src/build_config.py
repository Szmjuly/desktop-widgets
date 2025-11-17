"""Build configuration helper - checks if licensing should be included."""
import json
from pathlib import Path


def should_include_licensing():
    """Check if licensing code should be included based on build config."""
    build_config_file = Path(__file__).parent.parent / 'build_config.json'
    
    # Default to True if config doesn't exist (backward compatibility)
    if not build_config_file.exists():
        return True
    
    try:
        with open(build_config_file, 'r') as f:
            config = json.load(f)
            return config.get('include_licensing', True)
    except (json.JSONDecodeError, IOError):
        # On error, default to True (include licensing)
        return True


# Export a constant for easy checking
INCLUDE_LICENSING = should_include_licensing()

