// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace TiltBrush
{

    public class LightingButton : BaseButton
    {
        private TiltBrush.Environment m_Preset;
        private LightingPopUpWindow m_Owner;

        public void SetOwner(LightingPopUpWindow owner)
        {
            m_Owner = owner;
        }

        override protected void ConfigureTextureAtlas()
        {
            if (SketchControlsScript.m_Instance.AtlasIconTextures)
            {
                // Lighting icons are assigned later.  We want atlasing on all our
                // buttons, so just set it to the default for now.
                RefreshAtlasedMaterial();
            }
            else
            {
                base.ConfigureTextureAtlas();
            }
        }

        public void SetPreset(TiltBrush.Environment rPreset)
        {
            m_Preset = rPreset;

            SetButtonTexture(m_Preset.m_IconTexture);
            SetDescriptionText(m_Preset.Description);
        }

        override protected void OnButtonPressed()
        {
            if (m_Preset.m_IsCategory)
            {
                if (m_Owner != null)
                {
                    m_Owner.OpenCategoryPopup(m_Preset);
                }
                return;
            }
            if (SceneSettings.m_Instance.IsTransitioning &&
                SceneSettings.m_Instance.GetDesiredPreset() == m_Preset)
            {
                return;
            }
            if (LightsControlScript.m_Instance.LightsChanged ||
                SceneSettings.m_Instance.EnvironmentChanged ||
                SceneSettings.m_Instance.CurrentEnvironment != m_Preset)
            {
                SceneSettings.m_Instance.RecordSkyColorsForFading();
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new SwitchEnvironmentCommand(m_Preset));
            }
        }
    }
} // namespace TiltBrush
