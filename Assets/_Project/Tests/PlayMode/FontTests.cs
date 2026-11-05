using System.Collections.Generic;
using HR.Core;
using HR.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace HR.Tests.PlayMode
{
    public sealed class FontTests
    {
        static readonly FontRole[] Roles = { FontRole.Display, FontRole.Text };

        const string Cyrillic = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ"
                                + "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";

        const string Latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        const string Digits = "0123456789";

        const string Punctuation = "«»:.,!?-";

        GameObject _host;

        [SetUp]
        public void SetUp()
        {
            UiFonts.Forget();
            _host = new GameObject("FontHost", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            UiFonts.Forget();
        }

        [Test]
        public void Both_fonts_are_in_the_build_and_are_not_the_fallback()
        {
            var display = UiFonts.For(FontRole.Display);
            var text = UiFonts.For(FontRole.Text);

            Assert.IsNotNull(display);
            Assert.IsNotNull(text);

            StringAssert.Contains("Unbounded", display.name,
                "the display font did not load and fell back to the built-in one");
            StringAssert.Contains("Manrope", text.name,
                "the body font did not load and fell back to the built-in one");
            Assert.AreNotSame(display, text, "both roles resolved to the same font");
        }

        [Test]
        public void Each_font_can_draw_every_letter_the_game_uses()
        {
            foreach (var role in Roles)
            {
                var font = UiFonts.For(role);
                font.RequestCharactersInTexture(Cyrillic + Latin + Digits + Punctuation);

                foreach (char c in Cyrillic + Latin + Digits + Punctuation)
                    Assert.IsTrue(font.HasCharacter(c),
                        $"{role} ({font.name}) has no glyph for '{c}' (U+{(int)c:X4})");
            }
        }

        [Test]
        public void The_display_font_carries_the_play_triangle()
        {
            var display = UiFonts.For(FontRole.Display);
            Assert.IsTrue(display.HasCharacter('▶'),
                "the continue button is a Display label and needs its own triangle");
        }

        const float ColumnWidth = 814f;

        const float CardWidth = 750f;

        const float SettingsRowWidth = 602f;

        const float ThemeCardWidth = 568f;

        static readonly (string Text, int Size, FontRole Role, float Width)[] Longest =
        {
            ("Параметры конфиденциальности", UiBuilder.BodySize, FontRole.Text, SettingsRowWidth),
            ("Privacy settings", UiBuilder.BodySize, FontRole.Text, SettingsRowWidth),
            ("Чувствительность", UiBuilder.BodySize, FontRole.Text, SettingsRowWidth),
            ("Инверсия вращения", UiBuilder.BodySize, FontRole.Text, SettingsRowWidth),
            ("Рекорд в обычной игре 30", UiBuilder.BodySize, FontRole.Text, ColumnWidth),
            ("Каждого кольца можно коснуться только раз", UiBuilder.BodySize, FontRole.Text,
             ColumnWidth),
            ("Each ring may be touched only once", UiBuilder.BodySize, FontRole.Text, ColumnWidth),
            ("Открыта тема «Ретро ЭЛТ»", UiBuilder.BodySize, FontRole.Text, CardWidth),
            ("▶ Продолжить · реклама", UiBuilder.ButtonSize, FontRole.Display, CardWidth),
            ("Сбросить прогресс", UiBuilder.ButtonSize, FontRole.Display, CardWidth),
            ("Продолжить", UiBuilder.ButtonSize, FontRole.Display, CardWidth),
            ("ХАРДКОР", UiBuilder.ButtonSize, FontRole.Display, ColumnWidth),
            ("Hypnotic Rings", UiBuilder.TitleSize, FontRole.Display, ColumnWidth),
            ("Настройки", UiBuilder.TitleSize, FontRole.Display, ColumnWidth),
            ("Ретро ЭЛТ", UiBuilder.ButtonSize, FontRole.Display, ThemeCardWidth),
            ("Неон-космос", UiBuilder.ButtonSize, FontRole.Display, ThemeCardWidth),
        };

        [Test]
        public void The_longest_string_of_every_role_still_fits_at_its_best_fit_floor()
        {
            var shrunk = new List<string>();

            foreach (var (content, size, role, width) in Longest)
            {
                var label = UiBuilder.CreateLabel("Probe", _host.transform, content, size,
                                                  role: role);
                int floor = UiFonts.MinSizeFor(size);

                var box = new Vector2(width, Mathf.Ceil(size * 1.45f));
                ((RectTransform)label.transform).sizeDelta = box;

                var settings = label.GetGenerationSettings(box);
                var generator = label.cachedTextGenerator;
                generator.PopulateWithErrors(content, settings, _host);

                int used = generator.fontSizeUsedForBestFit;
                Assert.Greater(used, 0, $"'{content}' produced no glyphs at all");

                if (used < floor)
                    shrunk.Add($"'{content}' fell to {used}, floor is {floor} (max {size})");

                Object.DestroyImmediate(label.gameObject);
            }

            Assert.IsEmpty(shrunk, string.Join("; ", shrunk));
        }
    }
}
