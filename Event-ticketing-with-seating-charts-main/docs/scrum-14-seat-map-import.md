# SCRUM-14 Seat-map import

Upload a JSON file with `multipart/form-data` to:

```http
PUT /api/performances/{performanceId}/seat-map
```

The form field name is `file`. The JSON document uses this format:

```json
{
  "seats": [
    { "row": "A", "number": "1", "category": "VIP" },
    { "row": "A", "number": "2", "category": "VIP" },
    { "row": "B", "number": "1", "category": "Standard" }
  ]
}
```

Rows, seat numbers, and categories are required. A map supports up to 20,000 seats. Seat positions must be unique within a performance.

The import creates missing categories by case-insensitive name. If the performance already has a map, all old seats are replaced and categories no longer used by that performance are removed. Replacement is rejected with HTTP `409` when an old seat is `SOLD` or has a `HELD` status whose `held_until` is still in the future.

Category replacement, old-map deletion, and batched seat insertion run in one database transaction. A failure in any batch rolls back the whole import.
