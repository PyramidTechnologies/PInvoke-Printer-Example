NumericUpDownLib
----------------

This library implements more than 10 different numeric up down WPF controls that can be used to edit values:
- with a textbox or
- up/down arrow (repeat) buttons
- up/down cursor keys
- spinning mousewheel up down on mouseover

Implements specific numeric up down control for data type:
- byte    (ByteUpDown    control)
- decimal (DecimalUpDown control)
- double  (DoubleUpDown  control)
- float   (FloatUpDown   control)
- integer (IntegerUpDown control)
- long    (LongUpDown    control)
- sbyte   (SbyteUpDown   control)
- short   (ShortUpDown   control)
- ushort  (UshortUpDown  control)
- uint    (UintUpDown    control)
- ulong   (UlongUpDown   control)

Percentages can be edit at [0-100] while backend viewmodels handles [0-1] values,
see FactorToDoubleConverter and PercentageUpDownDemo in demo clients at project site.

Controls are fully themeable. Project site contains demos for:
- Dark/Light theme and
- Generics theme
test clients.

More Features:
- Increments and Decrements can be configured to be 1 or any greater value than 1.
- The width of the control can be configured to be fixed (textbox will scroll inside when text is too large)
- Up/Down button is disabled when min or max limit is already reached
- SelectAll on GotFocus of TextBox
- Spin value up/down on mouseover + mousewheel spin

There is a demo application that shows the usage of the control (LIght/Black themes enabled) and documents the features,
such as, the ability to configure a minimum and maximum value that can be used to keep the resulting
value within a given bound.
