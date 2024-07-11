using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.UI.Fields
{
    public class FieldEnum : Field
    {
        public Dropdown Dropdown;

        public void Setup(Type type)
        {
            Dropdown.ClearOptions();
            Dropdown.AddOptions(Enum.GetNames(type).ToList());
        }
	}
}
