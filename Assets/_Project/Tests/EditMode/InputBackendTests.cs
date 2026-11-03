using System.Collections.Generic;
using HR.Editor;
using NUnit.Framework;

namespace HR.Tests.EditMode
{
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

            Assert.Pass();
        }

        [Test]
        public void The_project_settings_are_what_project_setup_declares()
        {
            List<string> problems = ProjectSetup.Verify();
            Assert.IsEmpty(problems, "project settings drifted:\n  " + string.Join("\n  ", problems));
        }
    }
}
