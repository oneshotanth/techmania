using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoonSharp.Interpreter;
using UnityEngine.UIElements;
using System.Reflection;

namespace ThemeApi
{
    // VisualElement and related classes are wrapped in these API
    // class because:
    // - Lua doesn't support generics or extension methods
    // - Lua functions aren't automatically converted to Actions
    //
    // Note that it's possible to create multiple wraps on the same
    // VisualElement.
    [MoonSharpUserData]
    public class VisualElementWrap
    {
        public VisualElement inner { get; private set; }

        [MoonSharpHidden]
        public VisualElementWrap(VisualElement e)
        {
            inner = e;
        }

        #region Properties
        public int childCount => inner.childCount;
        public bool enabledInHierarchy => inner.enabledInHierarchy;
        public bool enabledSelf => inner.enabledSelf;
        public string name => inner.name;
        public VisualElementWrap parent =>
            new VisualElementWrap(inner.parent);
        public bool visible => inner.visible;
        // If false, this element will ignore pointer events.
        public bool pickable
        {
            get { return inner.pickingMode == PickingMode.Position; }
            set
            {
                inner.pickingMode = value ?
                    PickingMode.Position : PickingMode.Ignore;
            }
        }

        public void SetEnabled(bool enabled)
        {
            inner.SetEnabled(enabled);
        }

        public IResolvedStyle resolvedStyle => inner.resolvedStyle;
        public IStyle style => inner.style;
        #endregion

        #region Subclass-specific properties
        public void CheckType(System.Type type, string targetMember)
        {
            if (!type.IsAssignableFrom(inner.GetType()))
            {
                throw new System.Exception($"VisualElement {name} is not a {type.Name}, and therefore does not have the '{targetMember}' member.");
            }
        }

        public bool IsTextElement() { return inner is TextElement; }
        public bool IsButton() { return inner is Button; }
        public bool IsToggle() { return inner is Toggle; }

        public string text => (inner as TextElement).text;

        public float lowValue
        {
            get
            {
                if (inner is Slider)
                    return (inner as Slider).lowValue;
                if (inner is SliderInt)
                    return (inner as SliderInt).lowValue;
                throw new System.Exception($"VisualElement {name} is neither a Slider or a SliderInt, and therefore does not have the 'lowValue' member.");
            }
            set
            {
                if (inner is Slider)
                    (inner as Slider).lowValue = value;
                if (inner is SliderInt)
                    (inner as SliderInt).lowValue = (int)value;
                throw new System.Exception($"VisualElement {name} is neither a Slider or a SliderInt, and therefore does not have the 'lowValue' member.");
            }
        }

        public float highValue
        {
            get
            {
                if (inner is Slider)
                    return (inner as Slider).highValue;
                if (inner is SliderInt)
                    return (inner as SliderInt).highValue;
                throw new System.Exception($"VisualElement {name} is neither a Slider or a SliderInt, and therefore does not have the 'highValue' member.");
            }
            set
            {
                if (inner is Slider)
                    (inner as Slider).highValue = value;
                if (inner is SliderInt)
                    (inner as SliderInt).highValue = (int)value;
                throw new System.Exception($"VisualElement {name} is neither a Slider or a SliderInt, and therefore does not have the 'highValue' member.");
            }
        }
        #endregion

        #region Events
        // https://docs.unity3d.com/2021.2/Documentation/Manual/UIE-Events-Reference.html
        // Exposed as the "eventType" global table
        public enum EventType
        {
            // Capture events: omitted

            // Change events
            ChangeBool,
            ChangeInt,
            ChangeFloat,

            // Command events: omitted

            // Drag events
            DragExited,
            DragUpdated,
            DragPerform,
            DragEnter,
            DragLeave,

            // Focus events
            FocusOut,
            FocusIn,
            Blur,
            Focus,

            // Input events
            Input,

            // Keyboard events
            KeyDown,
            KeyUp,

            // Layout events
            GeometryChanged,

            // Pointer & mouse events
            // (mouse fires both, touchscreen only fires pointer)
            PointerDown,
            PointerUp,
            PointerMove,
            PointerEnter,
            PointerLeave,
            PointerOver,
            PointerOut,
            PointerStationary,
            PointerCancel,
            Click,
            Wheel,
            
            // Panel events
            AttachToPanel,
            DetachFromPanel,

            // Tooltip events
            Tooptip,

            // Unity events
            FrameUpdate,
            ApplicationFocus,
        }

        private System.Type EventTypeEnumToType(EventType t)
        {
            return t switch
            {
                EventType.ChangeBool => typeof(ChangeEvent<bool>),
                EventType.ChangeInt => typeof(ChangeEvent<int>),
                EventType.ChangeFloat => typeof(ChangeEvent<float>),
                EventType.Click => typeof(ClickEvent),
                EventType.FrameUpdate => typeof(FrameUpdateEvent),
                EventType.ApplicationFocus =>
                    typeof(ApplicationFocusEvent),
                _ => throw new System.Exception(
                    "Unsupported event type: " + t)
            };
        }

        // Callback parameters:
        // 1. The VisualElementWrap receiving the event
        // 2. The event
        // 3. The data (Void if called without this parameters)
        public void RegisterCallback(EventType eventType,
            DynValue callback, DynValue data)
        {
            callback.CheckType("VisualElementWrap.RegisterCallback",
                DataType.Function);
            System.Type genericType = EventTypeEnumToType(eventType);
            switch (eventType)
            {
                case EventType.FrameUpdate:
                    UnityEventSynthesizer.AddListener
                        <FrameUpdateEvent>(inner);
                    break;
                case EventType.ApplicationFocus:
                    UnityEventSynthesizer.AddListener
                        <ApplicationFocusEvent>(inner);
                    break;
            }
            MethodInfo methodInfo = typeof(CallbackRegistry)
                .GetMethod("AddCallback",
                BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(genericType);
            methodInfo.Invoke(null, new object[] {
                inner, callback, data });
        }

        public void UnregisterCallback(EventType eventType,
            DynValue callback)
        {
            callback.CheckType(
                "VisualElementWrap.UnregisterCallback",
                DataType.Function);
            System.Type genericType = EventTypeEnumToType(eventType);
            switch (eventType)
            {
                case EventType.FrameUpdate:
                    UnityEventSynthesizer.RemoveListener
                        <FrameUpdateEvent>(inner);
                    break;
                case EventType.ApplicationFocus:
                    UnityEventSynthesizer.RemoveListener
                        <ApplicationFocusEvent>(inner);
                    break;
                default:
                    throw new System.Exception("Unsupported event type: " + eventType);
            }
            MethodInfo methodInfo = typeof(CallbackRegistry)
                .GetMethod("RemoveCallback",
                BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(genericType);
            methodInfo.Invoke(null, new object[] {
                inner, callback });
        }
        #endregion

        #region Query
        // className is optional, even in Lua.
        public VisualElementWrap Q(string name,
            string className = null)
        {
            return new VisualElementWrap(inner.Q(name, className));
        }

        // Leave out `name` to query all elements.
        public UQueryStateWrap Query(string name = null,
            string className = null)
        {
            return new UQueryStateWrap(inner.Query(
                name, className).Build());
        }
        #endregion

        #region Class manipulation
        public IEnumerable<string> GetClasses()
            => inner.GetClasses();
        public bool ClassListContains(string className)
            => inner.ClassListContains(className);
        public void AddToClassList(string className)
            => inner.AddToClassList(className);
        public void RemoveFromClassList(string className)
            => inner.RemoveFromClassList(className);
        public void ClearClassList()
            => inner.ClearClassList();
        public void EnableInClassList(string className, bool enable)
            => inner.EnableInClassList(className, enable);
        public void ToggleInClassList(string className)
            => inner.ToggleInClassList(className);
        #endregion

        #region Style shortcuts
        public void SetDisplay(bool display)
        {
            style.display = display ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        public void SetVisibility(bool visible)
        {
            style.visibility = visible ? Visibility.Visible
                : Visibility.Hidden;
        }
        #endregion
    }

    [MoonSharpUserData]
    public class UQueryStateWrap
    {
        public UQueryState<VisualElement> inner { get; private set; }
        [MoonSharpHidden]
        public UQueryStateWrap(UQueryState<VisualElement> s)
        {
            inner = s;
        }

        public void ForEach(DynValue f)
        {
            f.CheckType("UQueryStateApi.ForEach", DataType.Function);
            inner.ForEach((VisualElement e) =>
            {
                f.Function.Call(new VisualElementWrap(e));
            });
        }
    }
}