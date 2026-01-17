# Fix Report

## Summary
Selecting a child group in the left NavigationView did not update the Library filter or the "current group" label. The page stayed on the "all skills" view.

## Root Cause
The group items are created at runtime and placed under `LibraryNavItem.MenuItems`. In WPF-UI 3.0.5, only changes to the top-level `NavigationView.MenuItems` trigger internal registration. Child `MenuItems` updates do not call `AddItemsToDictionaries`, so the new group items are never marked as `IsMenuElement` and are not registered in the navigation dictionaries.

Because `NavigationView` only raises `SelectionChanged` when the active item is a menu element, clicking a group navigates but does not update `SelectedItem` or fire `SelectionChanged`. As a result, `MainWindow.NavigationView_SelectionChanged` never calls `LibraryViewModel.SelectGroupById`, leaving the filter at "all skills".

## Fix
Expose a small helper on a derived NavigationView to register dynamically added child items, then call it after rebuilding the group list:

- `Views/SkillNavigationView.cs`: new subclass that calls protected `UpdateMenuItemsTemplate` and `AddItemsToDictionaries`.
- `Views/MainWindow.xaml`: use the subclass instead of the base `ui:NavigationView`, and explicitly apply the base `NavigationView` style so the control template loads.
- `Views/MainWindow.xaml.cs`: after rebuilding group items, call `NavigationView.RegisterNestedMenuItems(LibraryNavItem.MenuItems)`, and update `NavigationView_SelectionChanged` to accept a `NavigationView` sender (required by the typed event signature).

This registers the new child items, sets `IsMenuElement = true`, and enables `SelectionChanged` to fire for group clicks, which updates the filter and group label correctly.

## Files Changed
- Views/SkillNavigationView.cs
- Views/MainWindow.xaml
- Views/MainWindow.xaml.cs

## Notes / Verification
No automated tests or UI run were performed. Manual verification steps:
1. Launch the app.
2. Click "Skill Library" -> should show all skills and label "current group: all skills".
3. Click a child group (e.g. unity) -> list filters to that group and label updates.
4. Click "Skill Library" again -> list returns to all skills.

## Follow-up Startup Crash
After introducing `SkillNavigationView`, the app crashed at startup with `NullReferenceException` in `NavigationView.UpdateContent`. Root cause: `SkillNavigationView` (a derived control) did not inherit the default `NavigationView` style, so its template parts were never created. `NavigationViewContentPresenter` remained null and `Navigate(...)` failed.

Fix applied:
- `Views/SkillNavigationView.cs`: override `DefaultStyleKey` metadata to reuse the base `NavigationView` style.
- `Views/MainWindow.xaml`: apply `Style="{DynamicResource {x:Type ui:NavigationView}}"` to force the base template.
