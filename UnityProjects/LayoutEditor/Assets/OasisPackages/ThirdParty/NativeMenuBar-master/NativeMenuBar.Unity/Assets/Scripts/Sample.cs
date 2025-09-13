using NativeMenuBar.Core;
using System.Linq;
using TMPro;
using UnityEngine;

public class Sample : MonoBehaviour
{
    [SerializeField]
    private MenuBar menuBar = default;

    [SerializeField]
    private TextMeshProUGUI item1Text = default;

    [SerializeField]
    private TextMeshProUGUI subItem1Text = default;

    [SerializeField]
    private TextMeshProUGUI subItem2Text = default;

    private void Start()
    {
        var disabledMenuItem = menuBar.MenuItems.FirstOrDefault(item => item.Name == "Disabled Item");
        disabledMenuItem.IsInteractable = false;
    }

    #region MenuItems Handlers
    public void OnBasicItem1MenuItemClicked()
    {
        item1Text.gameObject.SetActive(true);
    }

    public void OnBasicSubItem1MenuItemClicked()
    {
        subItem1Text.gameObject.SetActive(true);
    }

    public void OnBasicSubItem2MenuItemClicked()
    {
        subItem2Text.gameObject.SetActive(true);
    }

    public void OnToggleToggle1MenuItemClicked()
    {
        var menuItem = menuBar.MenuItems.FirstOrDefault(item => item.Name == "Toggle 1");
        menuItem.IsToggled = !menuItem.IsToggled;
    }

    public void OnToggleToggle2MenuItemClicked()
    {
        var menuItem = menuBar.MenuItems.FirstOrDefault(item => item.Name == "Toggle 2");
        menuItem.IsToggled = !menuItem.IsToggled;
    }

    public void OnToggleToggle3MenuItemClicked()
    {
        var menuItem = menuBar.MenuItems.FirstOrDefault(item => item.Name == "Toggle 3");
        menuItem.IsToggled = !menuItem.IsToggled;
    }

    public void OnDisabledToggleEnabledDisabledMenuItemClicked()
    {
        var menuItem = menuBar.MenuItems.FirstOrDefault(item => item.Name == "Disabled Item");
        menuItem.IsInteractable = !menuItem.IsInteractable;
    }
    
    #endregion
}
