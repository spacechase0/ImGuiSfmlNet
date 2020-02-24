using ImGuiNET;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using OpenTK.Graphics.OpenGL;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ImGuiSfmlNet
{
    public class ImGuiSfml
    {
        private static bool s_windowHasFocus = false;
        private static bool[] s_mousePressed = new bool[3] { false, false, false };
        private static bool[] s_touchDown = new bool[3] { false, false, false };
        private static bool s_mouseMoved = false;
        private static Vector2i s_touchPos;
        private static Texture s_fontTexture;

        private static readonly uint NULL_JOYSTICK_ID = Joystick.Count;
        private static uint s_joystickId = NULL_JOYSTICK_ID;

        private static readonly uint NULL_JOYSTICK_BUTTON = Joystick.ButtonCount;
        private static uint[] s_joystickMapping = new uint[(int)ImGuiNavInput.COUNT];
        
        private struct StickInfo
        {
            public Joystick.Axis xAxis;
            public Joystick.Axis yAxis;

            public bool xInverted;
            public bool yInverted;

            public float threshold;
        }

        private static StickInfo s_dPadInfo;
        private static StickInfo s_lStickInfo;

        private static Cursor[] s_mouseCursors = new Cursor[(int)ImGuiMouseCursor.COUNT];
        private static bool[] s_mouseCursorLoaded = new bool[(int)ImGuiMouseCursor.COUNT];

        public static void Init(RenderWindow window, bool loadDefaultFont = true)
        {
            Init(window, window, loadDefaultFont);
        }

        public static void Init(RenderWindow window, RenderTarget target, bool loadDefaultFont = true)
        {
            Init(window, new Vector2f(target.Size.X, target.Size.Y), loadDefaultFont);
        }

        public static void Init(RenderWindow window, Vector2f displaySize, bool loadDefaultFont = true)
        {
            ImGui.CreateContext();
            var io = ImGui.GetIO();

            io.BackendFlags |= ImGuiBackendFlags.HasGamepad;
            io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
            io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
            //io.BackendPlatformName = "imgui_impl_sfml_net";

            unsafe
            {
                io.KeyMap[(int)ImGuiKey.Tab] = (int)Keyboard.Key.Tab;
                io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Keyboard.Key.Left;
                io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Keyboard.Key.Right;
                io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Keyboard.Key.Up;
                io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Keyboard.Key.Down;
                io.KeyMap[(int)ImGuiKey.PageUp] = (int)Keyboard.Key.PageUp;
                io.KeyMap[(int)ImGuiKey.PageDown] = (int)Keyboard.Key.PageDown;
                io.KeyMap[(int)ImGuiKey.Home] = (int)Keyboard.Key.Home;
                io.KeyMap[(int)ImGuiKey.End] = (int)Keyboard.Key.End;
                io.KeyMap[(int)ImGuiKey.Insert] = (int)Keyboard.Key.Insert;
                io.KeyMap[(int)ImGuiKey.Delete] = (int)Keyboard.Key.Delete;
                io.KeyMap[(int)ImGuiKey.Backspace] = (int)Keyboard.Key.Backspace;
                io.KeyMap[(int)ImGuiKey.Space] = (int)Keyboard.Key.Space;
                io.KeyMap[(int)ImGuiKey.Enter] = (int)Keyboard.Key.Enter;
                io.KeyMap[(int)ImGuiKey.Escape] = (int)Keyboard.Key.Escape;
                io.KeyMap[(int)ImGuiKey.A] = (int)Keyboard.Key.A;
                io.KeyMap[(int)ImGuiKey.C] = (int)Keyboard.Key.C;
                io.KeyMap[(int)ImGuiKey.V] = (int)Keyboard.Key.V;
                io.KeyMap[(int)ImGuiKey.X] = (int)Keyboard.Key.X;
                io.KeyMap[(int)ImGuiKey.Y] = (int)Keyboard.Key.Y;
                io.KeyMap[(int)ImGuiKey.Z] = (int)Keyboard.Key.Z;
            }

            s_joystickId = GetConnectedJoystickId();

            for ( uint i = 0; i < (int) ImGuiNavInput.COUNT; ++i )
            {
                s_joystickMapping[i] = NULL_JOYSTICK_BUTTON;
            }

            InitDefaultJoystickMapping();

            io.DisplaySize = new System.Numerics.Vector2(displaySize.X, displaySize.Y);

            unsafe
            {
                io.SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate( setClipboardTextDelegate = new SetClipboardTextDelegate(SetClipboardText));
                io.GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate( getClipboardTextDelegate = new GetClipboardTextDelegate(GetClipboardText));
            }

            for ( int i = 0; i < (int)ImGuiMouseCursor.COUNT; ++i )
            {
                s_mouseCursorLoaded[i] = false;
            }

            LoadMouseCursor(ImGuiMouseCursor.Arrow, Cursor.CursorType.Arrow);
            LoadMouseCursor(ImGuiMouseCursor.TextInput, Cursor.CursorType.Text);
            LoadMouseCursor(ImGuiMouseCursor.ResizeAll, Cursor.CursorType.SizeAll);
            LoadMouseCursor(ImGuiMouseCursor.ResizeNS, Cursor.CursorType.SizeVertical);
            LoadMouseCursor(ImGuiMouseCursor.ResizeEW, Cursor.CursorType.SizeHorinzontal);
            LoadMouseCursor(ImGuiMouseCursor.ResizeNESW, Cursor.CursorType.SizeBottomLeftTopRight);
            LoadMouseCursor(ImGuiMouseCursor.ResizeNWSE, Cursor.CursorType.SizeTopLeftBottomRight);
            LoadMouseCursor(ImGuiMouseCursor.Hand, Cursor.CursorType.Hand);

            if (s_fontTexture != null)
                s_fontTexture = null;
            s_fontTexture = new Texture(1, 1);

            if (loadDefaultFont)
                UpdateFontTexture();

            s_windowHasFocus = window.HasFocus();

            // TODO: Move these to their own functions, and deregister in Shutdown
            window.MouseMoved += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                s_mouseMoved = true;
            };
            window.MouseButtonReleased += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                if ((int)e.Button >= 0 && (int)e.Button < 3)
                    s_mousePressed[(int)e.Button] = true;
            };
            window.TouchBegan += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                s_mouseMoved = false;
                if (e.Finger >= 0 && e.Finger < 3)
                    s_touchDown[e.Finger] = true;
            };
            window.TouchEnded += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                s_mouseMoved = false;
            };
            window.MouseWheelScrolled += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                if (e.Wheel == Mouse.Wheel.VerticalWheel || e.Wheel == Mouse.Wheel.HorizontalWheel && io.KeyShift)
                    io.MouseWheel += e.Delta;
                else if (e.Wheel == Mouse.Wheel.HorizontalWheel)
                    io.MouseWheelH += e.Delta;
            };
            window.KeyPressed += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                io.KeysDown[(int)e.Code] = true;
            };
            window.KeyPressed += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                io.KeysDown[(int)e.Code] = false;
            };
            window.TextEntered += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                if (e.Unicode[0] < ' ' || e.Unicode[0] == 127)
                    return;
                io.AddInputCharacter((uint)e.Unicode[0]);
            };
            window.JoystickConnected += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                if (s_joystickId == NULL_JOYSTICK_ID)
                    s_joystickId = e.JoystickId;
            };
            window.JoystickDisconnected += (s, e) =>
            {
                if (!window.HasFocus())
                    return;

                if (s_joystickId == e.JoystickId)
                    s_joystickId = GetConnectedJoystickId();
            };
            window.LostFocus += (s, e) => s_windowHasFocus = false;
            window.GainedFocus += (s, e) => s_windowHasFocus = true;
        }

        public static void Update(RenderWindow window, Time dt)
        {
            Update(window, window, dt);
        }

        public static void Update(Window window, RenderTarget target, Time dt)
        {
            UpdateMouseCursor(window);

            if ( !s_mouseMoved )
            {
                if (Touch.IsDown(0))
                    s_touchPos = Touch.GetPosition(0, window);

                Update(s_touchPos, new Vector2f(target.Size.X, target.Size.Y), dt);
            }
            else
            {
                Update(Mouse.GetPosition(window), new Vector2f(target.Size.X, target.Size.Y), dt);
            }

            if ( ImGui.GetIO().MouseDrawCursor )
            {
                window.SetMouseCursorVisible(false);
            }
        }

        public static void Update(Vector2i mousePos, Vector2f displaySize, Time dt)
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new System.Numerics.Vector2(displaySize.X, displaySize.Y);
            io.DeltaTime = dt.AsSeconds();

            if ( s_windowHasFocus )
            {
                if (io.WantSetMousePos)
                    Mouse.SetPosition(new Vector2i((int)io.MousePos.X, (int) io.MousePos.Y));
                else
                    io.MousePos = new System.Numerics.Vector2(mousePos.X, mousePos.Y);
                
                for ( int i = 0; i < 3; ++i )
                {
                    io.MouseDown[i] = s_touchDown[i] || Touch.IsDown((uint)i) || s_mousePressed[i] || Mouse.IsButtonPressed((Mouse.Button)i);
                    s_mousePressed[i] = false;
                    s_touchDown[i] = false;
                }
            }

            io.KeyCtrl = io.KeysDown[(int)Keyboard.Key.LControl] || io.KeysDown[(int)Keyboard.Key.RControl];
            io.KeyAlt = io.KeysDown[(int)Keyboard.Key.LAlt] || io.KeysDown[(int)Keyboard.Key.RAlt];
            io.KeyShift = io.KeysDown[(int)Keyboard.Key.LShift] || io.KeysDown[(int)Keyboard.Key.RShift];
            io.KeySuper = io.KeysDown[(int)Keyboard.Key.LSystem] || io.KeysDown[(int)Keyboard.Key.RSystem];

            if ( ( io.ConfigFlags & ImGuiConfigFlags.NavEnableGamepad ) != 0 && s_joystickId != NULL_JOYSTICK_ID )
            {
                UpdateJoystickActionState(io, ImGuiNavInput.Activate);
                UpdateJoystickActionState(io, ImGuiNavInput.Cancel);
                UpdateJoystickActionState(io, ImGuiNavInput.Input);
                UpdateJoystickActionState(io, ImGuiNavInput.Menu);

                UpdateJoystickActionState(io, ImGuiNavInput.FocusPrev);
                UpdateJoystickActionState(io, ImGuiNavInput.FocusNext);
                
                UpdateJoystickActionState(io, ImGuiNavInput.TweakSlow);
                UpdateJoystickActionState(io, ImGuiNavInput.TweakFast);

                UpdateJoystickDPadState(io);
                UpdateJoystickLStickState(io);
            }

            ImGui.NewFrame();
        }

        public static void Render(RenderTarget target)
        {
            target.ResetGLStates();
            ImGui.Render();
            RenderDrawLists(target, ImGui.GetDrawData());
        }

        /*
        public static void Render()
        {
            ImGui.Render();
            RenderDrawLists(ImGui.GetDrawData());
        }*/

        public static void Shutdown()
        {
            ImGui.GetIO().Fonts.TexID = IntPtr.Zero;

            if (s_fontTexture != null)
                s_fontTexture = null;

            for ( int i = 0; i < (int) ImGuiMouseCursor.COUNT; ++i )
            {
                if ( s_mouseCursorLoaded[i] )
                {
                    s_mouseCursors[i] = null;
                    s_mouseCursorLoaded[i] = false;
                }
            }

            ImGui.DestroyContext();
        }

        unsafe public static void UpdateFontTexture()
        {
            var io = ImGui.GetIO();
            byte* pixels_;
            int width, height;

            io.Fonts.GetTexDataAsRGBA32(out pixels_, out width, out height);
            byte[] pixels = new byte[width * height * 4];
            Marshal.Copy((IntPtr)pixels_, pixels, 0, width * height * 4);

            s_fontTexture = new Texture((uint)width, (uint)height);
            s_fontTexture.Update(pixels);

            io.Fonts.TexID = ConvertGLTextureHandleToImTextureID(s_fontTexture.NativeHandle);
        }

        public static Texture GetFontTexture() { return s_fontTexture; }

        public static void SetActiveJoystickId(uint joystickId)
        {
            s_joystickId = joystickId;
        }

        public static void SetJoystickDPadThreshold(float threshold)
        {
            s_dPadInfo.threshold = threshold;
        }

        public static void SetJoystickLStickThreshold(float threshold)
        {
            s_lStickInfo.threshold = threshold;
        }

        public static void SetJoystickMapping(int action, uint joystickButton)
        {
            s_joystickMapping[action] = joystickButton;
        }

        public static void SetDPadXAxis(Joystick.Axis dPadXAxis, bool inverted = false)
        {
            s_dPadInfo.xAxis = dPadXAxis;
            s_dPadInfo.xInverted = inverted;
        }

        public static void SetDPadYAxis(Joystick.Axis dPadYAxis, bool inverted = false)
        {
            s_dPadInfo.yAxis = dPadYAxis;
            s_dPadInfo.yInverted = inverted;
        }

        public static void SetLStickXAxis(Joystick.Axis lStickXAxis, bool inverted = false)
        {
            s_lStickInfo.xAxis = lStickXAxis;
            s_lStickInfo.xInverted = inverted;
        }

        public static void SetLStickYAxis(Joystick.Axis lStickYAxis, bool inverted = false)
        {
            s_lStickInfo.yAxis = lStickYAxis;
            s_lStickInfo.yInverted = inverted;
        }

        private static Vector2 GetTopLeftAbsolute(FloatRect rect)
        {
            Vector2 pos = ImGui.GetCursorScreenPos();
            return new Vector2(rect.Left + pos.X, rect.Top + pos.Y);
        }

        private static Vector2 GetDownRightAbsolute(FloatRect rect)
        {
            Vector2 pos = ImGui.GetCursorScreenPos();
            return new Vector2(rect.Left + rect.Width + pos.X, rect.Top + rect.Height + pos.Y);
        }

        private static IntPtr ConvertGLTextureHandleToImTextureID(uint glTextureHandle)
        {
            return (IntPtr)glTextureHandle;
        }

        private static uint ConvertImTextureIDToGLTextureHandle(IntPtr textureID)
        {
            return (uint)textureID.ToInt64();
        }

        private delegate void DrawCmdUserCallback(ImDrawListPtr cmd_list, ImDrawCmdPtr pcmd);

        unsafe private static void RenderDrawLists(RenderTarget target, ImDrawDataPtr draw_data)
        {
            ImGui.GetDrawData();
            if (draw_data.CmdListsCount == 0)
                return;

            var oldView = target.GetView();
            //target.SetView(new View(new FloatRect(0, 0, target.Size.X, target.Size.Y)));

            var io = ImGui.GetIO();

            int fb_width = (int)(io.DisplaySize.X * io.DisplayFramebufferScale.X);
            int fb_height = (int)(io.DisplaySize.Y * io.DisplayFramebufferScale.Y);
            draw_data.ScaleClipRects(io.DisplayFramebufferScale);

            /*
            for (int n = 0; n < draw_data.CmdListsCount; ++n)
            {
                var cmd_list = draw_data.CmdListsRange[n];
                var vtx_buffer = cmd_list.VtxBuffer;
                var idx_buffer = cmd_list.IdxBuffer;

                for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; ++cmd_i)
                {
                    var pcmd = cmd_list.CmdBuffer[cmd_i];
                    if (pcmd.UserCallback != IntPtr.Zero)
                        Marshal.GetDelegateForFunctionPointer<DrawCmdUserCallback>(pcmd.UserCallback)(cmd_list, pcmd);
                    else
                    {
                        var vertices = new Vertex[pcmd.ElemCount];
                        for ( int i = 0; i < pcmd.ElemCount; ++i )
                        {
                            var vert = vtx_buffer[idx_buffer[i]];
                            Vertex v;
                            v.Position = new Vector2f(vert.pos.X, vert.pos.Y);
                            v.TexCoords = new Vector2f(vert.uv.X, vert.uv.Y);
                            v.Color = new Color((byte)((vert.col >> 0) & 0xFF), (byte)((vert.col >> 8) & 0xFF), (byte)((vert.col >> 16) & 0xFF), (byte)((vert.col >> 24) & 0xFF));
                            vertices[i] = v;
                        }
                        
                        //View view = new View(new FloatRect((int)(pcmd.ClipRect.X), (int)(fb_height - pcmd.ClipRect.W), (int)(pcmd.ClipRect.Z - pcmd.ClipRect.X), (int)(pcmd.ClipRect.W - pcmd.ClipRect.Y)));
                        //target.SetView(view);
                        // todo: texturing
                        target.Draw(vertices, SFML.Graphics.PrimitiveType.Triangles);
                    }
                }
            }

            target.SetView(oldView);
            //*/

            //*
            GL.PushAttrib(AttribMask.EnableBit | AttribMask.ColorBufferBit | AttribMask.TransformBit);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.ScissorTest);
            GL.Enable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Lighting);
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.ColorArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);

            GL.Viewport(0, 0, fb_width, fb_height);

            GL.MatrixMode(MatrixMode.Texture);
            GL.LoadIdentity();

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            GL.Ortho(0, io.DisplaySize.X, io.DisplaySize.Y, 0, -1, 1);

            for ( int n = 0; n < draw_data.CmdListsCount; ++n )
            {
                var cmd_list = draw_data.CmdListsRange[n];
                var vtx_buffer = cmd_list.VtxBuffer.Data;
                var idx_buffer = cmd_list.IdxBuffer.Data;

                GL.VertexPointer(2, VertexPointerType.Float, sizeof(ImDrawVert), vtx_buffer + Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.pos)).ToInt32());
                GL.TexCoordPointer(2, TexCoordPointerType.Float, sizeof(ImDrawVert), vtx_buffer + Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.uv)).ToInt32());
                GL.ColorPointer(4, ColorPointerType.UnsignedByte, sizeof(ImDrawVert), vtx_buffer + Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.col)).ToInt32());

                for ( int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; ++cmd_i)
                {
                    var pcmd = cmd_list.CmdBuffer[cmd_i];
                    if (pcmd.UserCallback != IntPtr.Zero)
                        Marshal.GetDelegateForFunctionPointer<DrawCmdUserCallback>(pcmd.UserCallback)(cmd_list, pcmd);
                    else
                    {
                        uint textureHandle = ConvertImTextureIDToGLTextureHandle(pcmd.TextureId);
                        GL.BindTexture(TextureTarget.Texture2D, textureHandle);
                        GL.Scissor((int)(pcmd.ClipRect.X), (int)(fb_height - pcmd.ClipRect.W), (int)(pcmd.ClipRect.Z - pcmd.ClipRect.X), (int)(pcmd.ClipRect.W - pcmd.ClipRect.Y));
                        GL.DrawElements(OpenTK.Graphics.OpenGL.PrimitiveType.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, idx_buffer);
                    }
                }
            }

            GL.PopAttrib();
            //*/
        }

        private static uint GetConnectedJoystickId()
        {
            for ( uint i = 0; i < Joystick.Count; ++i )
            {
                if (Joystick.IsConnected(i))
                    return i;
            }
            return NULL_JOYSTICK_ID;
        }

        private static void InitDefaultJoystickMapping()
        {
            SetJoystickMapping((int)ImGuiNavInput.Activate, 0);
            SetJoystickMapping((int)ImGuiNavInput.Cancel, 1);
            SetJoystickMapping((int)ImGuiNavInput.Input, 3);
            SetJoystickMapping((int)ImGuiNavInput.Menu, 2);
            SetJoystickMapping((int)ImGuiNavInput.FocusPrev, 4);
            SetJoystickMapping((int)ImGuiNavInput.FocusNext, 5);
            SetJoystickMapping((int)ImGuiNavInput.TweakSlow, 4);
            SetJoystickMapping((int)ImGuiNavInput.TweakFast, 5);

            SetDPadXAxis(Joystick.Axis.PovX, false);
            SetDPadYAxis(Joystick.Axis.PovY, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? true : false);

            SetLStickXAxis(Joystick.Axis.X);
            SetLStickYAxis(Joystick.Axis.Y);

            SetJoystickDPadThreshold(5);
            SetJoystickLStickThreshold(5);
        }

        private static void UpdateJoystickActionState(ImGuiIOPtr io, ImGuiNavInput action)
        {
            io.NavInputs[(int)action] = Joystick.IsButtonPressed(s_joystickId, s_joystickMapping[(int)action]) ? 1 : 0;
        }

        private static void UpdateJoystickDPadState(ImGuiIOPtr io)
        {
            float dpadXPos = Joystick.GetAxisPosition(s_joystickId, s_dPadInfo.xAxis);
            if (s_dPadInfo.xInverted)
                dpadXPos = -dpadXPos;

            float dpadYPos = Joystick.GetAxisPosition(s_joystickId, s_dPadInfo.yAxis);
            if (s_dPadInfo.yInverted)
                dpadYPos = -dpadYPos;

            io.NavInputs[(int)ImGuiNavInput.DpadLeft ] = dpadXPos < -s_dPadInfo.threshold ? 1 : 0;
            io.NavInputs[(int)ImGuiNavInput.DpadRight] = dpadXPos >  s_dPadInfo.threshold ? 1 : 0;
            io.NavInputs[(int)ImGuiNavInput.DpadUp   ] = dpadYPos < -s_dPadInfo.threshold ? 1 : 0;
            io.NavInputs[(int)ImGuiNavInput.DpadDown ] = dpadYPos >  s_dPadInfo.threshold ? 1 : 0;
        }

        private static void UpdateJoystickLStickState(ImGuiIOPtr io)
        {
            float lStickXPos = Joystick.GetAxisPosition(s_joystickId, s_lStickInfo.xAxis);
            if (s_lStickInfo.xInverted)
                lStickXPos = -lStickXPos;

            float lStickYPos = Joystick.GetAxisPosition(s_joystickId, s_lStickInfo.yAxis);
            if (s_lStickInfo.yInverted)
                lStickYPos = -lStickYPos;

            if (lStickXPos < -s_lStickInfo.threshold)
                io.NavInputs[(int)ImGuiNavInput.LStickLeft] = Math.Abs(lStickXPos / 100);
            if (lStickXPos >  s_lStickInfo.threshold)
                io.NavInputs[(int)ImGuiNavInput.LStickRight] = lStickXPos / 100;
            if (lStickYPos < -s_lStickInfo.threshold)
                io.NavInputs[(int)ImGuiNavInput.LStickLeft] = Math.Abs(lStickYPos / 100);
            if (lStickYPos >  s_lStickInfo.threshold)
                io.NavInputs[(int)ImGuiNavInput.LStickRight] = lStickYPos / 100;
        }

        private unsafe delegate void SetClipboardTextDelegate(void* userData, string text);
        private static SetClipboardTextDelegate setClipboardTextDelegate;
        unsafe private static void SetClipboardText(void* userData, string text)
        {
            Clipboard.Contents = text;
        }

        private unsafe delegate string GetClipboardTextDelegate(void* userData);
        private static GetClipboardTextDelegate getClipboardTextDelegate;
        unsafe private static string GetClipboardText(void* userData)
        {
            return Clipboard.Contents;
        }

        private static void LoadMouseCursor(ImGuiMouseCursor imguiCursorType, Cursor.CursorType sfmlCursorType)
        {
            s_mouseCursors[(int)imguiCursorType] = new Cursor(sfmlCursorType);
            s_mouseCursorLoaded[(int)imguiCursorType] = true;
        }

        private static void UpdateMouseCursor(Window window)
        {
            var io = ImGui.GetIO();
            if ( (io.ConfigFlags & ImGuiConfigFlags.NoMouseCursorChange) == 0 )
            {
                ImGuiMouseCursor cursor = ImGui.GetMouseCursor();
                if (io.MouseDrawCursor || cursor == ImGuiMouseCursor.None)
                    window.SetMouseCursorVisible(false);
                else
                {
                    window.SetMouseCursorVisible(true);

                    Cursor c = s_mouseCursorLoaded[(int)cursor] ? s_mouseCursors[(int)cursor] : s_mouseCursors[(int)ImGuiMouseCursor.Arrow];
                    window.SetMouseCursor(c);
                }
            }
        }
    }
}
