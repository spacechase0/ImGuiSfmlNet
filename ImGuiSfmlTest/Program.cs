using ImGuiNET;
using ImGuiSfmlNet;
using SFML.Graphics;
using SFML.System;
using System;

namespace ImGuiSfmlTest
{
    class Program
    {
        static void Main(string[] args)
        {
            // NOTE : This is workaround to create a functioning opengl context for OpenTK (for current OpenTK version)
            var gameWindow = new OpenTK.GameWindow();

            var window = new RenderWindow(new SFML.Window.VideoMode(640, 480), "ImGui + SFML + .Net = <3");
            window.SetFramerateLimit(60);
            ImGuiSfml.Init(window);

            window.Closed += (s, e) => window.Close();

            CircleShape shape = new CircleShape(100);
            shape.FillColor = Color.Green;

            Clock deltaClock = new Clock();
            while ( window.IsOpen )
            {
                window.DispatchEvents();

                ImGuiSfml.Update(window, deltaClock.Restart());

                ImGui.ShowDemoWindow();
                //ImGui.ShowTestWindow();

                /*
                ImGui.Begin("Hello, world!");
                ImGui.Button("Look at this pretty button");
                ImGui.End();
                */

                window.Clear();
                window.Draw(shape);
                ImGuiSfml.Render(window);
                window.Display();
            }
        }
    }
}
