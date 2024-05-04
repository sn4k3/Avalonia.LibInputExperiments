using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Input;
using Avalonia.Input.Raw;
using static Avalonia.LibInputExperiments.LibInputNativeUnsafeMethods;
using static Avalonia.LibInputExperiments.LibXkbCommonNativeUnsafeMethods;

namespace Avalonia.LibInputExperiments;

public partial class LibInputBackend
{
  private void SetupKeyboard(LibInputKeyboardConfiguration configuration)
  {
    _keyboard = AvaloniaLocator.CurrentMutable.GetService<IKeyboardDevice>();

    _xkbContext = xkb_context_new(XkbContextFlags.XKB_CONTEXT_NO_FLAGS);
    var names = new XkbRuleNames
    {
      Rules = configuration.Rules,
      Model = configuration.Model,
      Layout = configuration.Layout,
      Variant = configuration.Variant,
      Options = configuration.Options
    };
    _xkbKeymap = xkb_keymap_new_from_names(
      _xkbContext,
      ref names,
      XkbKeymapCompileFlags.XKB_KEYMAP_COMPILE_NO_FLAGS
    );

    _modifiers = FindModifiers(_xkbKeymap).ToArray();

    _xkbState = xkb_state_new(_xkbKeymap);
  }

  private static IEnumerable<Modifier> FindModifiers(IntPtr xkbKeymap)
  {
    // For an unknown reason xkb_keymap_mod_get_name works but xkb_keymap_mod_get_index does not.
    var modifiersCount = xkb_keymap_num_mods(xkbKeymap);
    var modifiersIndexesByName = (
      from index in Enumerable.Range(0, (int)modifiersCount)
      let name = xkb_keymap_mod_get_name(xkbKeymap, (uint)index)
      select (index, name)
    ).ToDictionary(x => x.name, x => (uint)x.index);
    
    return
      from mod in new[]
      {
        ("Alt", RawInputModifiers.Alt),
        ("Control", RawInputModifiers.Control),
        ("Shift", RawInputModifiers.Shift),
        ("Meta", RawInputModifiers.Meta)
      }
      let index = modifiersIndexesByName.GetValueOrDefault(mod.Item1, uint.MaxValue)
      where index != uint.MaxValue
      select new Modifier(index, mod.Item1, mod.Item2);
  }

  private IntPtr _xkbContext = IntPtr.Zero;
  private IntPtr _xkbKeymap = IntPtr.Zero;
  private IntPtr _xkbState = IntPtr.Zero;

  record Modifier(uint Index, string Name, RawInputModifiers Raw);

  private Modifier[] _modifiers;
  private IKeyboardDevice _keyboard;

  private void HandleKeyboard(IntPtr ev, LibInputEventType type)
  {
    var kbEv = libinput_event_get_keyboard_event(ev);
    if (kbEv == IntPtr.Zero)
      return;
    
    var scancode = libinput_event_keyboard_get_key(kbEv);
    var keycode = scancode + 8;
    var state = libinput_event_keyboard_get_key_state(kbEv);
    
    if (state == LibInputKeyState.LIBINPUT_KEY_STATE_PRESSED)
      xkb_state_update_key(_xkbState, keycode, XkbKeyDirection.XKB_KEY_DOWN);
    else if (state == LibInputKeyState.LIBINPUT_KEY_STATE_RELEASED)
      xkb_state_update_key(_xkbState, keycode, XkbKeyDirection.XKB_KEY_UP);

    var symbol = new StringBuilder();
    xkb_state_key_get_utf8(_xkbState, keycode, symbol, 64);

    var keySym = xkb_state_key_get_one_sym(_xkbState, keycode);
    
    var modifiers = RawInputModifiers.None;
    foreach (var modifier in _modifiers)
      if (xkb_state_mod_index_is_active(_xkbState, modifier.Index, XkbStateComponent.XKB_STATE_MODS_EFFECTIVE) != 0)
        modifiers |= modifier.Raw;

#if DEBUG
    var keysymName = new StringBuilder();
    xkb_keysym_get_name(keySym, keysymName, 64);
    Console.WriteLine($"KEYBOARD {scancode} {symbol} {keySym} {keysymName}");
#endif

    if (_inputRoot == null)
      return;
    
    var args = new RawKeyEventArgs(
      _keyboard,
      libinput_event_keyboard_get_time_usec(kbEv),
      _inputRoot,
      state == LibInputKeyState.LIBINPUT_KEY_STATE_PRESSED
        ? RawKeyEventType.KeyDown
        : RawKeyEventType.KeyUp,
      SymToKey(keySym),
      modifiers,
      ScanCodeToPhysicalKey(scancode),
      symbol.ToString()
      );
    ScheduleInput(args);
  }

  // ReSharper disable once CyclomaticComplexity
  private static PhysicalKey ScanCodeToPhysicalKey(uint scanCode) =>
    scanCode switch
    {
      001 => PhysicalKey.Escape,
      002 => PhysicalKey.Digit1,
      003 => PhysicalKey.Digit2,
      004 => PhysicalKey.Digit3,
      005 => PhysicalKey.Digit4,
      006 => PhysicalKey.Digit5,
      007 => PhysicalKey.Digit6,
      008 => PhysicalKey.Digit7,
      009 => PhysicalKey.Digit8,
      010 => PhysicalKey.Digit9,
      011 => PhysicalKey.Digit0,
      012 => PhysicalKey.Minus,
      013 => PhysicalKey.Equal,
      014 => PhysicalKey.Backspace,
      015 => PhysicalKey.Tab,
      016 => PhysicalKey.Q,
      017 => PhysicalKey.W,
      018 => PhysicalKey.E,
      019 => PhysicalKey.R,
      020 => PhysicalKey.T,
      021 => PhysicalKey.Y,
      022 => PhysicalKey.U,
      023 => PhysicalKey.I,
      024 => PhysicalKey.O,
      025 => PhysicalKey.P,
      026 => PhysicalKey.BracketLeft,
      027 => PhysicalKey.BracketRight,
      028 => PhysicalKey.Enter,
      029 => PhysicalKey.ControlLeft,
      030 => PhysicalKey.A,
      031 => PhysicalKey.S,
      032 => PhysicalKey.D,
      033 => PhysicalKey.F,
      034 => PhysicalKey.G,
      035 => PhysicalKey.H,
      036 => PhysicalKey.J,
      037 => PhysicalKey.K,
      038 => PhysicalKey.L,
      039 => PhysicalKey.Semicolon,
      040 => PhysicalKey.Quote,
      041 => PhysicalKey.Backquote,
      042 => PhysicalKey.ShiftLeft,
      043 => PhysicalKey.Backslash,
      044 => PhysicalKey.Z,
      045 => PhysicalKey.X,
      046 => PhysicalKey.C,
      047 => PhysicalKey.V,
      048 => PhysicalKey.B,
      049 => PhysicalKey.N,
      050 => PhysicalKey.M,
      051 => PhysicalKey.Comma,
      052 => PhysicalKey.Period,
      053 => PhysicalKey.Slash,
      054 => PhysicalKey.ShiftRight,
      055 => PhysicalKey.NumPadMultiply,
      056 => PhysicalKey.AltLeft,
      057 => PhysicalKey.Space,
      058 => PhysicalKey.CapsLock,
      059 => PhysicalKey.F1,
      060 => PhysicalKey.F2,
      061 => PhysicalKey.F3,
      062 => PhysicalKey.F4,
      063 => PhysicalKey.F5,
      064 => PhysicalKey.F6,
      065 => PhysicalKey.F7,
      066 => PhysicalKey.F8,
      067 => PhysicalKey.F9,
      068 => PhysicalKey.F10,
      069 => PhysicalKey.NumLock,
      070 => PhysicalKey.ScrollLock,
      071 => PhysicalKey.NumPad7,
      072 => PhysicalKey.NumPad8,
      073 => PhysicalKey.NumPad9,
      074 => PhysicalKey.NumPadSubtract,
      075 => PhysicalKey.NumPad4,
      076 => PhysicalKey.NumPad5,
      077 => PhysicalKey.NumPad6,
      078 => PhysicalKey.NumPadAdd,
      079 => PhysicalKey.NumPad1,
      080 => PhysicalKey.NumPad2,
      081 => PhysicalKey.NumPad3,
      082 => PhysicalKey.NumPad0,
      083 => PhysicalKey.NumPadDecimal,
      084 => PhysicalKey.None,
      085 => PhysicalKey.None,
      086 => PhysicalKey.IntlBackslash,
      087 => PhysicalKey.F11,
      088 => PhysicalKey.F12,  
      089 => PhysicalKey.None,
      090 => PhysicalKey.None,
      091 => PhysicalKey.None,
      092 => PhysicalKey.None,
      093 => PhysicalKey.None,
      094 => PhysicalKey.None,
      095 => PhysicalKey.None,
      096 => PhysicalKey.None,
      097 => PhysicalKey.ControlRight,
      098 => PhysicalKey.NumPadDivide,
      099 => PhysicalKey.PrintScreen,
      100 => PhysicalKey.AltRight,
      101 => PhysicalKey.None,
      102 => PhysicalKey.Home,
      103 => PhysicalKey.ArrowUp,
      104 => PhysicalKey.PageUp,
      105 => PhysicalKey.ArrowLeft,
      106 => PhysicalKey.ArrowRight,
      107 => PhysicalKey.End,
      108 => PhysicalKey.ArrowDown,
      109 => PhysicalKey.PageDown,
      110 => PhysicalKey.Insert,
      111 => PhysicalKey.Delete,
      112 => PhysicalKey.None,
      113 => PhysicalKey.AudioVolumeMute,
      114 => PhysicalKey.AudioVolumeDown,
      115 => PhysicalKey.AudioVolumeUp,
      116 => PhysicalKey.None,
      117 => PhysicalKey.None,
      118 => PhysicalKey.None,
      119 => PhysicalKey.Pause,
      120 => PhysicalKey.None,
      121 => PhysicalKey.None,
      122 => PhysicalKey.None,
      123 => PhysicalKey.None,
      124 => PhysicalKey.None,
      125 => PhysicalKey.MetaLeft,
      126 => PhysicalKey.None,
      127 => PhysicalKey.ContextMenu,
      128 => PhysicalKey.None,
      129 => PhysicalKey.None,
      163 => PhysicalKey.MediaTrackNext,
      164 => PhysicalKey.MediaPlayPause,
      165 => PhysicalKey.MediaTrackPrevious,
      _ => PhysicalKey.None,
    };

  // ReSharper disable once CyclomaticComplexity
  private static Key SymToKey(uint keySym) =>
    keySym switch
    {
      065 => Key.A,
      066 => Key.B,
      067 => Key.C,
      068 => Key.D,
      069 => Key.E,
      070 => Key.F,
      071 => Key.G,
      072 => Key.H,
      073 => Key.I,
      074 => Key.J,
      075 => Key.K,
      076 => Key.L,
      077 => Key.M,
      078 => Key.N,
      079 => Key.O,
      080 => Key.P,
      081 => Key.Q,
      082 => Key.R,
      083 => Key.S,
      084 => Key.T,
      085 => Key.U,
      086 => Key.V,
      087 => Key.W,
      088 => Key.X,
      089 => Key.Y,
      090 => Key.Z,
      091 => Key.None,
      092 => Key.None,
      093 => Key.None,
      094 => Key.None,
      095 => Key.None,
      096 => Key.None,
      097 => Key.A,
      098 => Key.B,
      099 => Key.C,
      100 => Key.D,
      101 => Key.E,
      102 => Key.F,
      103 => Key.G,
      104 => Key.H,
      105 => Key.I,
      106 => Key.J,
      107 => Key.K,
      108 => Key.L,
      109 => Key.M,
      110 => Key.N,
      111 => Key.O,
      112 => Key.P,
      113 => Key.Q,
      114 => Key.R,
      115 => Key.S,
      116 => Key.T,
      117 => Key.U,
      118 => Key.V,
      119 => Key.W,
      120 => Key.X,
      121 => Key.Y,
      122 => Key.Z,
      _ => Key.None,
    };
}