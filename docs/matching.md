# Track Matching Strategy

## Priority
1. ISRC exact match
2. Normalized title + main artist + duration tolerance
3. Fuzzy score over title/artist tokens
4. Manual review bucket for low confidence

## Confidence levels
- High (>= 0.9): auto-accept
- Medium (0.7 - 0.89): auto-accept if no close competitor
- Low (< 0.7): manual review

## Guardrails
- Penalize mismatched tags (live/remix/karaoke/instrumental)
- Preserve source track order
- Cache accepted mappings for future jobs
