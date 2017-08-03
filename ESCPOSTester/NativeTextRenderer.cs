using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ESCPOSTester
{
    /// <summary>      
    /// Wrapper for GDI  text rendering functions<br/>      
    /// This class is  not thread-safe as GDI function should be called from the UI thread.      
    ///  </summary>      
    public sealed class NativeTextRenderer : IDisposable
    {
        #region Fields and Consts

        /// <summary>      
        /// used for <see  cref="MeasureString(string,System.Drawing.Font,float,out int,out  int)"/> calculation.      
        /// </summary>      
        private static readonly int[] _charFit = new int[1];

        /// <summary>      
        /// used for <see  cref="MeasureString(string,System.Drawing.Font,float,out int,out  int)"/> calculation.      
        /// </summary>      
        private static readonly int[] _charFitWidth = new int[1000];

        /// <summary>      
        /// cache of all the font used not to  create same font again and again      
        /// </summary>      
        private static readonly Dictionary<string, Dictionary<float, Dictionary<FontStyle, IntPtr>>> _fontsCache = new Dictionary<string, Dictionary<float, Dictionary<FontStyle, IntPtr>>>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>      
        /// The wrapped WinForms graphics object      
        /// </summary>      
        private readonly Graphics _g;

        /// <summary>      
        /// the initialized HDC used      
        /// </summary>      
        private IntPtr _hdc;

        #endregion


        /// <summary>      
        /// Init.      
        /// </summary>      
        public NativeTextRenderer(Graphics g)
        {
            _g = g;

            var clip = _g.Clip.GetHrgn(_g);

            _hdc = _g.GetHdc();
            RawPrinterHelper.SetBkMode(_hdc, 1);

            RawPrinterHelper.SelectClipRgn(_hdc, clip);

            RawPrinterHelper.DeleteObject(clip);
        }

        /// <summary>      
        /// Measure the width and height of string  <paramref name="str"/> when drawn on device context HDC      
        /// using the given font <paramref  name="font"/>.      
        /// </summary>      
        /// <param name="str">the  string to measure</param>      
        /// <param name="font">the  font to measure string with</param>      
        /// <returns>the size of the  string</returns>      
        public Size MeasureString(string str, Font font)
        {
            SetFont(font);

            var size = new Size();
            RawPrinterHelper.GetTextExtentPoint32(_hdc, str, str.Length, ref size);
            return size;
        }

        /// <summary>      
        /// Measure the width and height of string  <paramref name="str"/> when drawn on device context HDC      
        /// using the given font <paramref  name="font"/>.<br/>      
        /// Restrict the width of the string and  get the number of characters able to fit in the restriction and      
        /// the width those characters take.      
        /// </summary>      
        /// <param name="str">the  string to measure</param>      
        /// <param name="font">the  font to measure string with</param>      
        /// <param  name="maxWidth">the max width to render the string  in</param>      
        /// <param  name="charFit">the number of characters that will fit under  <see cref="maxWidth"/> restriction</param>      
        /// <param  name="charFitWidth"></param>      
        /// <returns>the size of the  string</returns>      
        public Size MeasureString(string str, Font font, float maxWidth, out int charFit, out int charFitWidth)
        {
            SetFont(font);

            var size = new Size();
            RawPrinterHelper.GetTextExtentExPoint(_hdc, str, str.Length, (int)Math.Round(maxWidth), _charFit, _charFitWidth, ref size);
            charFit = _charFit[0];
            charFitWidth = charFit > 0 ? _charFitWidth[charFit - 1] : 0;
            return size;
        }

        /// <summary>      
        /// Draw the given string using the given  font and foreground color at given location.      
        /// </summary>      
        /// <param name="str">the  string to draw</param>      
        /// <param name="font">the  font to use to draw the string</param>      
        /// <param name="color">the  text color to set</param>      
        /// <param name="point">the  location to start string draw (top-left)</param>      
        public void DrawString(String str, Font font, Color color, Point point)
        {
            SetFont(font);
            SetTextColor(color);

            RawPrinterHelper.TextOut(_hdc, point.X, point.Y, str, str.Length);
        }

        /// <summary>      
        /// Draw the given string using the given  font and foreground color at given location.<br/>      
        /// See [http://msdn.microsoft.com/en-us/library/windows/desktop/dd162498(v=vs.85).aspx][15].      
        /// </summary>      
        /// <param name="str">the  string to draw</param>      
        /// <param name="font">the  font to use to draw the string</param>      
        /// <param name="color">the  text color to set</param>      
        /// <param name="rect">the  rectangle in which the text is to be formatted</param>      
        /// <param name="flags">The  method of formatting the text</param>      
        public void DrawString(String str, Font font, Color color, RectangleF rect, RawPrinterHelper.TextFormatFlags flags)
        {
            SetFont(font);
            SetTextColor(color);

            var rect2 = new RawPrinterHelper.Rect(rect);
            RawPrinterHelper.DrawText(_hdc, str, str.Length, ref  rect2, (uint)flags);
        }

        /// <summary>      
        /// Release current HDC to be able to use  <see cref="Graphics"/> methods.      
        /// </summary>      
        public void Dispose()
        {
            if (_hdc != IntPtr.Zero)
            {
                RawPrinterHelper.SelectClipRgn(_hdc, IntPtr.Zero);
                _g.ReleaseHdc(_hdc);
                _hdc = IntPtr.Zero;
            }
        }


        #region Private methods

        /// <summary>      
        /// Set a resource (e.g. a font) for the  specified device context.      
        /// </summary>      
        private void SetFont(Font font)
        {
            RawPrinterHelper.SelectObject(_hdc, GetCachedHFont(font));
        }

        /// <summary>      
        /// Get cached unmanaged font handle for  given font.<br/>      
        /// </summary>      
        /// <param name="font">the  font to get unmanaged font handle for</param>      
        /// <returns>handle to unmanaged  font</returns>      
        private static IntPtr GetCachedHFont(Font font)
        {
            IntPtr hfont = IntPtr.Zero;
            Dictionary<float, Dictionary<FontStyle, IntPtr>> dic1;
            if (_fontsCache.TryGetValue(font.Name, out dic1))
            {
                Dictionary<FontStyle, IntPtr> dic2;
                if (dic1.TryGetValue(font.Size, out  dic2))
                {
                    dic2.TryGetValue(font.Style, out hfont);
                }
                else
                {
                    dic1[font.Size] = new Dictionary<FontStyle, IntPtr>();
                }
            }
            else
            {
                _fontsCache[font.Name] = new Dictionary<float, Dictionary<FontStyle, IntPtr>>();
                _fontsCache[font.Name][font.Size] = new Dictionary<FontStyle, IntPtr>();
            }

            if (hfont == IntPtr.Zero)
            {
                _fontsCache[font.Name][font.Size][font.Style] = hfont = font.ToHfont();
            }

            return hfont;
        }

        /// <summary>      
        /// Set the text color of the device  context.      
        /// </summary>      
        private void SetTextColor(Color color)
        {
            int rgb = (color.B & 0xFF) << 16 | (color.G & 0xFF) << 8 | color.R;
            RawPrinterHelper.SetTextColor(_hdc, rgb);
        }    
        #endregion
    }
}
