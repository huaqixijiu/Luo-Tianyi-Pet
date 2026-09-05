using System.Text.Json;

namespace LuoTianyiPet.Core;

public static class BodyHitMapJsonParser
{
    public static BodyHitMap Parse(string json, string expectedAnimationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedAnimationId);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        RequireNumber(root, "schemaVersion", 1);
        RequireString(root, "coordinateSpace", "normalized-image");
        RequireString(root, "hitTestRule", "first-matching-region");

        JsonElement model = RequireProperty(root, "model", JsonValueKind.Object);
        RequireString(model, "animationId", expectedAnimationId);
        JsonElement regionsElement = RequireProperty(root, "regions", JsonValueKind.Array);

        List<(int Priority, BodyHitRegion Region)> orderedRegions = [];
        HashSet<BodyRegionId> regionIds = [];
        HashSet<int> priorities = [];
        foreach (JsonElement regionElement in regionsElement.EnumerateArray())
        {
            string idValue = RequireProperty(regionElement, "id", JsonValueKind.String).GetString()!;
            if (!Enum.TryParse(idValue, ignoreCase: false, out BodyRegionId regionId) ||
                !Enum.IsDefined(regionId))
            {
                throw new JsonException($"Unknown body region id: {idValue}.");
            }
            if (!regionIds.Add(regionId))
            {
                throw new JsonException($"Duplicate body region id: {idValue}.");
            }

            int priority = RequireProperty(regionElement, "priority", JsonValueKind.Number).GetInt32();
            if (priority < 0 || !priorities.Add(priority))
            {
                throw new JsonException($"Invalid or duplicate priority for body region: {idValue}.");
            }

            JsonElement polygonsElement = RequireProperty(regionElement, "polygons", JsonValueKind.Array);
            List<NormalizedPolygon> polygons = [];
            foreach (JsonElement polygonElement in polygonsElement.EnumerateArray())
            {
                List<PointerPoint> vertices = [];
                foreach (JsonElement pointElement in polygonElement.EnumerateArray())
                {
                    if (pointElement.ValueKind != JsonValueKind.Array || pointElement.GetArrayLength() != 2)
                    {
                        throw new JsonException("Every polygon vertex must contain exactly two numbers.");
                    }

                    JsonElement.ArrayEnumerator coordinates = pointElement.EnumerateArray();
                    coordinates.MoveNext();
                    double x = coordinates.Current.GetDouble();
                    coordinates.MoveNext();
                    double y = coordinates.Current.GetDouble();
                    vertices.Add(new PointerPoint(x, y));
                }

                try
                {
                    polygons.Add(new NormalizedPolygon(vertices));
                }
                catch (ArgumentException exception)
                {
                    throw new JsonException($"Invalid polygon for body region: {idValue}.", exception);
                }
            }

            try
            {
                orderedRegions.Add((priority, BodyHitRegion.FromPolygons(regionId, polygons)));
            }
            catch (ArgumentException exception)
            {
                throw new JsonException($"Invalid body region: {idValue}.", exception);
            }
        }

        if (regionIds.Count != Enum.GetValues<BodyRegionId>().Length)
        {
            throw new JsonException("The body map must define every body region exactly once.");
        }

        return new BodyHitMap(
            orderedRegions
                .OrderBy(item => item.Priority)
                .Select(item => item.Region)
                .ToArray());
    }

    private static JsonElement RequireProperty(
        JsonElement parent,
        string name,
        JsonValueKind expectedKind)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != expectedKind)
        {
            throw new JsonException($"Required {name} property is missing or invalid.");
        }

        return value;
    }

    private static void RequireString(JsonElement parent, string name, string expected)
    {
        string? actual = RequireProperty(parent, name, JsonValueKind.String).GetString();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new JsonException($"Unsupported {name}: {actual}.");
        }
    }

    private static void RequireNumber(JsonElement parent, string name, int expected)
    {
        int actual = RequireProperty(parent, name, JsonValueKind.Number).GetInt32();
        if (actual != expected)
        {
            throw new JsonException($"Unsupported {name}: {actual}.");
        }
    }
}
