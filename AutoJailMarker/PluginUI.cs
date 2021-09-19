using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using AutoJailMarker;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;

namespace AutoJailMarker
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;

        private ImGuiScene.TextureWrap goatImage;

        private AutoJailMarker parent;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        // passing in the image here just for simplicity
        public PluginUI(Configuration configuration, ImGuiScene.TextureWrap goatImage, AutoJailMarker p)
        {
            this.configuration = configuration;
            this.goatImage = goatImage;
            this.parent = p;
        }

        public void Dispose()
        {
            this.goatImage.Dispose();
        }

        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawMainWindow();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 330), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("Auto Jail Markers", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                for (int i = 0; i < 8; i++)
                {
                    if (ImGui.InputText(i.ToString(), ref configuration.prio[i], 256))
                    {
                        configuration.Save();
                    }
                }

                if (ImGui.Button("try partylist"))
                {
                    parent.UpdateOrderedParty();
                }

                ImGui.Text($"Current party size: {DalamudApi.PartyList.Length.ToString()}");
                if (AutoJailMarker.PlayerExists)
                {
                    foreach(PartyMember p in DalamudApi.PartyList)
                    {
                        ImGui.Text(p.Name.ToString());
                        ImGui.SameLine();
                        ImGui.Text($"   OID: {p.ObjectId.ToString()}");
                    }
                }

                ImGui.Spacing();

                ImGui.Text("---------------");
                ImGui.Indent(10);
                ImGui.Image(this.goatImage.ImGuiHandle, new Vector2(this.goatImage.Width, this.goatImage.Height));
                ImGui.Unindent(10);

                ImGui.Text($"isCollecting: {parent.isCollecting.ToString()}");
                ImGui.Text($"Marked: {parent.marked.ToString()}");
            }
            ImGui.End();
        }

        public void DrawSettingsWindow()
        {
            if (!SettingsVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(232, 75), ImGuiCond.Always);
            if (ImGui.Begin("A Wonderful Configuration Window", ref this.settingsVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                // can't ref a property, so use a local copy
                var configValue = this.configuration.SomePropertyToBeSavedAndWithADefault;
                if (ImGui.Checkbox("Random Config Bool", ref configValue))
                {
                    this.configuration.SomePropertyToBeSavedAndWithADefault = configValue;
                    // can save immediately on change, if you don't want to provide a "Save and Close" button
                    this.configuration.Save();
                }
            }
            ImGui.End();
        }
    }
}
