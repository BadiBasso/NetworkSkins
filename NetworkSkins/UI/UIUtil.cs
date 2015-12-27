﻿using ColossalFramework.UI;
using UnityEngine;

namespace NetworkSkins.UI
{
    public class UIUtil
    {
        private const float LABEL_RELATIVE_WIDTH = .30f;
        private const float COLUMN_PADDING = 5f;

        public static UIDropDown CreateDropDownWithLabel(UIComponent parent, string labelText, float posY, float width) 
        {
            var labelWidth = Mathf.Round(width * LABEL_RELATIVE_WIDTH);
            
            UIDropDown dropDown = UIUtil.CreateDropDown(parent);
            dropDown.relativePosition = new Vector3(labelWidth + COLUMN_PADDING, posY);
            dropDown.width = width - labelWidth - COLUMN_PADDING;

            AddLabel(parent, labelText, labelWidth, dropDown.height, posY);

            return dropDown;
        }
        private static void AddLabel(UIComponent parent, string text, float width, float dropDownHeight, float y)
        {
            var label = parent.AddUIComponent<UILabel>();
            label.text = text;
            label.textScale = .85f;
            label.textColor = new Color32(200, 200, 200, 255);
            label.autoSize = false;
            label.width = width;
            label.textAlignment = UIHorizontalAlignment.Right;
            label.relativePosition = new Vector3(0, y + Mathf.Round((dropDownHeight - label.height) / 2));
        }

        public static UIDropDown CreateDropDown(UIComponent parent)
        {
            UIDropDown dropDown = parent.AddUIComponent<UIDropDown>();
            dropDown.size = new Vector2(90f, 30f);
            dropDown.listBackground = "GenericPanelLight";
            dropDown.itemHeight = 30;
            dropDown.itemHover = "ListItemHover";
            dropDown.itemHighlight = "ListItemHighlight";
            dropDown.normalBgSprite = "ButtonMenu";
            dropDown.disabledBgSprite = "ButtonMenuDisabled";
            dropDown.hoveredBgSprite = "ButtonMenuHovered";
            dropDown.focusedBgSprite = "ButtonMenu";
            dropDown.listWidth = 90;
            dropDown.listHeight = 500;
            dropDown.foregroundSpriteMode = UIForegroundSpriteMode.Stretch;
            dropDown.popupColor = new Color32(45, 52, 61, 255);
            dropDown.popupTextColor = new Color32(170, 170, 170, 255);
            dropDown.zOrder = 1;
            dropDown.textScale = 0.8f;
            dropDown.verticalAlignment = UIVerticalAlignment.Middle;
            dropDown.horizontalAlignment = UIHorizontalAlignment.Left;
            dropDown.selectedIndex = 0;
            dropDown.textFieldPadding = new RectOffset(8, 0, 8, 0);
            dropDown.itemPadding = new RectOffset(14, 0, 8, 0);

            UIButton button = dropDown.AddUIComponent<UIButton>();
            dropDown.triggerButton = button;
            button.text = "";
            button.size = dropDown.size;
            button.relativePosition = new Vector3(0f, 0f);
            button.textVerticalAlignment = UIVerticalAlignment.Middle;
            button.textHorizontalAlignment = UIHorizontalAlignment.Left;
            button.normalFgSprite = "IconDownArrow";
            button.hoveredFgSprite = "IconDownArrowHovered";
            button.pressedFgSprite = "IconDownArrowPressed";
            button.focusedFgSprite = "IconDownArrowFocused";
            button.disabledFgSprite = "IconDownArrowDisabled";
            button.foregroundSpriteMode = UIForegroundSpriteMode.Fill;
            button.horizontalAlignment = UIHorizontalAlignment.Right;
            button.verticalAlignment = UIVerticalAlignment.Middle;
            button.zOrder = 0;
            button.textScale = 0.8f;

            dropDown.eventSizeChanged += new PropertyChangedEventHandler<Vector2>((c, t) =>
            {
                button.size = t; dropDown.listWidth = (int)t.x;
            });

            return dropDown;
        }
    }
}