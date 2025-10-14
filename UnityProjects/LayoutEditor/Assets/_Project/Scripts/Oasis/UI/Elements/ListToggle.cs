﻿using Oasis.UI.RecycledList;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.UI.Elements
{
    [RequireComponent(typeof(Toggle))]
    public class ListToggle : RecycledListItem
    {
        [SerializeField]
        private TextMeshProUGUI label = null;

        [SerializeField]
        [HideInInspector]
        private Toggle toggle = null;

        public int Index { get; set; }

        public bool Interactable { get { return toggle.interactable; } set { toggle.interactable = value; } }

        public TextMeshProUGUI Label { get { return label; } set { label = value; } }

        public event Action<int, bool> OnToggled;

        private void Awake()
        {
            toggle.onValueChanged.AddListener(x => OnToggled?.Invoke(Index, x));
        }

        public void SetToggledWithoutNotify(bool toggled)
        {
            toggle.SetIsOnWithoutNotify(toggled);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            GetStandardComponents();
        }

        private void Reset()
        {
            GetStandardComponents();
        }

        private void GetStandardComponents()
        {
            if (toggle == null)
            {
                toggle = GetComponent<Toggle>();
            }
        }
#endif
    }
}
