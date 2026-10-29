using System.Collections.Generic;
using HR.Editor;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
    /// <summary>
    /// Which input backend the project is actually built against.
    ///
    /// The game reads the finger through UnityEngine.InputSystem, but the backend that feeds it
    /// is a project setting, and with the old Input Manager selected the package is handed no
    /// devices at all. Nothing throws and nothing logs: Touchscreen.current is null, every poll
    /// returns nothing, and the build is silently deaf on a phone — which is how a signed .aab
    /// reached a device with a menu that could not be pressed.
    /// </summary>
    public class InputBackendTests
    {
        [Test]
        public void The_editor_is_compiled_against_the_input_system_package()
        {
#if !ENABLE_INPUT_SYSTEM
            Assert.Fail("ENABLE_INPUT_SYSTEM is not defined, so this editor compiled against the "
                        + "old Input Manager. Run HR.Editor.CI.CIBootstrap and relaunch Unity; "
                        + "anything built from this session gets no input devices.");
#endif
            // Defined and compiled: nothing further to assert, the define is the whole claim.
            Assert.Pass();
        }

        [Test]
        public void The_project_settings_are_what_project_setup_declares()
        {
            // Covers activeInputHandler along with everything else ProjectSetup writes. Applying
            // a setting is not evidence that it took — reading it back is.
            List<string> problems = ProjectSetup.Verify();
            Assert.IsEmpty(problems, "project settings drifted:\n  " + string.Join("\n  ", problems));
        }
    }
}
