using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Taums_inventory_hotkeys;

public sealed partial class ModEntry
{
    private static readonly ConditionalWeakTable<GameMenu, FieldInfo> InventoryPageFields = new();

    private static IClickableMenu? GetInventoryPage(GameMenu menu)
    {
        // Don't depend on the backing field name: Stardew's internal page
        // collection has changed between game builds. Find the inventory page
        // among the menu's collections, using the current tab index as fallback.
        if (InventoryPageFields.TryGetValue(menu, out var cachedField))
        {
            var cachedPage = FindInventoryPage(cachedField.GetValue(menu));
            if (cachedPage is not null)
                return cachedPage;
            InventoryPageFields.Remove(menu);
        }

        var currentTab = CurrentTabField?.GetValue(menu) as int? ?? 0;
        foreach (var field in GameMenuFields)
        {
            var value = field.GetValue(menu);
            if (value is IClickableMenu direct && direct.GetType().Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase))
            {
                InventoryPageFields.Add(menu, field);
                return direct;
            }
            if (value is not IEnumerable collection || value is string)
                continue;

            IClickableMenu? selectedPage = null;
            int pageIndex = 0;
            foreach (object? item in collection)
            {
                if (item is not IClickableMenu page)
                    continue;
                if (page.GetType().Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase))
                {
                    InventoryPageFields.Add(menu, field);
                    return page;
                }
                if (pageIndex++ == currentTab)
                    selectedPage = page;
            }
            if (selectedPage is not null)
                return selectedPage;
        }
        return null;
    }

    private static IClickableMenu? FindInventoryPage(object? value)
    {
        if (value is IClickableMenu direct)
            return direct.GetType().Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase) ? direct : null;
        if (value is not IEnumerable collection || value is string)
            return null;

        foreach (object? item in collection)
        {
            if (item is IClickableMenu page && page.GetType().Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase))
                return page;
        }
        return null;
    }

    private static List<Rectangle> GetNativeTabBounds(GameMenu menu, int tabY)
    {
        List<Rectangle> boundsFound = new();
        if (GetNativeTabComponents(menu) is IList tabs && tabs.Count > 0)
        {
            CollectNativeTabBounds(tabs, tabY, boundsFound);
            if (boundsFound.Count > 0)
                return boundsFound.OrderBy(bounds => bounds.Left).ToList();
        }

        foreach (var field in GameMenuFields)
        {
            if (field.GetValue(menu) is not IEnumerable values)
                continue;
            CollectNativeTabBounds(values, tabY, boundsFound);
        }
        return boundsFound.OrderBy(bounds => bounds.Left).ToList();
    }

    private static void CollectNativeTabBounds(IEnumerable values, int tabY, List<Rectangle> boundsFound)
    {
        foreach (var value in values)
        {
            if (value is not ClickableTextureComponent component || !component.visible)
                continue;
            var bounds = component.bounds;
            if (Math.Abs(bounds.Y - tabY) > 4 || bounds.Width < 48 || bounds.Width > 80)
                continue;
            if (!boundsFound.Contains(bounds))
                boundsFound.Add(bounds);
        }
    }

    private static int GetColumnSpacing(int[] anchors, int fallback)
    {
        var gaps = anchors.Distinct().OrderBy(x => x)
            .Zip(anchors.Distinct().OrderBy(x => x).Skip(1), (left, right) => right - left)
            .Where(gap => gap is > 0 and <= 96).ToArray();
        return gaps.Length == 0
            ? fallback
            : gaps.GroupBy(gap => gap).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).First().Key;
    }

    private static int[] GetInventoryColumnAnchors(GameMenu menu, int tabY)
    {
        var inventoryPage = GetInventoryPage(menu);
        if (inventoryPage is null)
            return Array.Empty<int>();

        List<Rectangle> components = new();
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        CollectPageComponentBounds(inventoryPage, components, visited, depth: 0);

        return components
            .Where(bounds => bounds.Y > tabY + 64 && bounds.Width >= 48 && bounds.Width <= 80)
            .GroupBy(bounds => bounds.Y)
            .Select(row => row.Select(bounds => bounds.Left).Distinct().OrderBy(x => x).ToArray())
            .Where(columns => columns.Length >= 10)
            .OrderByDescending(columns => columns.Length)
            .FirstOrDefault() ?? Array.Empty<int>();
    }

    private static void CollectPageComponentBounds(object? value, List<Rectangle> bounds, HashSet<object> visited, int depth)
    {
        if (value is null || depth > 5 || value is string || value.GetType().IsPrimitive)
            return;

        if (!value.GetType().IsValueType && !visited.Add(value))
            return;

        if (value is ClickableComponent component)
        {
            if (component.visible)
                bounds.Add(component.bounds);
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var child in enumerable)
                CollectPageComponentBounds(child, bounds, visited, depth + 1);
            return;
        }

        var type = value.GetType();
        if (type.Namespace?.StartsWith("StardewValley.Menus", StringComparison.Ordinal) != true)
            return;

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsStatic || field.FieldType.IsPrimitive || field.FieldType == typeof(string))
                continue;
            try
            {
                CollectPageComponentBounds(field.GetValue(value), bounds, visited, depth + 1);
            }
            catch (Exception)
            {
                // Ignore inaccessible or transient menu fields.
            }
        }
    }

    private static void RedrawNativeTabs(SpriteBatch b, GameMenu menu)
    {
        var tabY = menu.yPositionOnScreen + IClickableMenu.tabYPositionRelativeToMenuY + 64;
        if (GetNativeTabComponents(menu) is IList tabs && tabs.Count > 0)
        {
            if (DrawNativeTabComponents(b, tabs, tabY))
                return;
        }

        foreach (var field in GameMenuFields)
        {
            if (field.GetValue(menu) is not IEnumerable values)
                continue;
            DrawNativeTabComponents(b, values, tabY);
        }
    }

    private static bool DrawNativeTabComponents(SpriteBatch b, IEnumerable values, int tabY)
    {
        bool drewAny = false;
        foreach (var value in values)
        {
            if (value is ClickableTextureComponent component
                && component.visible
                && component.bounds.Y == tabY
                && component.bounds.Width >= 48
                && component.bounds.Width <= 80)
            {
                component.draw(b);
                drewAny = true;
            }
        }
        return drewAny;
    }

}
