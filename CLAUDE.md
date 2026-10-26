# Hypnotic Rings — правила проекта

Бесконечная аркада на 2D-физике: концентрические кольца сжимаются к центру,
игрок вращает их пальцем, шарик проваливается сквозь отверстия наружу.
Unity 6000.6.4f1, Android, портрет, оффлайн, бесплатно, без рекламы и покупок.

## Что читать первым после потери контекста

1. этот файл
2. `docs/PROGRESS.md` — где остановились
3. `docs/DECISIONS.md` — что уже решено и почему
4. `docs/CONTRACT.md` — механика, числа, строки, архитектура

## Приоритет документов при конфликте

`docs/design/GAME_DESIGN_SPEC.md` (числа, поведение, визуал) →
`docs/design/CLAUDE_CODE_PLAN.md` (порядок работ).

Поведение и числа — в `docs/GAME_DESIGN_SPEC.md`; при конфликте он главнее
GDD и плана. `docs/CONTRACT.md` — его рабочее прочтение: там же записаны все
места, где исходники молчали или спорили друг с другом.

`docs/design/` не редактируется никогда. Любое отступление от спецификации —
записью в `docs/DECISIONS.md` и только с доказательством (тест или
бот-симуляция).

## Команды

```sh
tools/preflight.sh          # окружение: Unity, Android-модуль, память, диск
tools/run_tests.sh          # EditMode + PlayMode из командной строки
tools/build_android.sh      # подписанный .aab (нужны переменные окружения)
tools/screenshots.sh        # скриншоты всех тем и экранов
tools/audit.sh              # TODO / FIXME / Debug.Log / секреты
python3 docs/publishing/check-listing.py   # пакет для стора против лимитов
```

Unity **никогда не запускается в фореграунде.** Каждый запуск — в фоне, с
логом в файл, под бюджетом памяти:

```sh
systemd-run --user --scope -q -p MemoryHigh=8G -p MemoryMax=10G -p CPUQuota=600% \
  nice -n 10 "$U/Unity" -batchmode -nographics -projectPath . \
  -executeMethod HR.Editor.ReleaseTool.CIBuild -logFile .logs/build.log
```

`-logFile /dev/stdout` запрещён: четверть миллиона строк и ни одной полезной.
`Library/Bee` не удалять — это разница между сборкой в минуты и в полчаса.

## Архитектурные ограничения

- **`HR.Core` не ссылается на UnityEngine.** Ни одной ссылки. Это проверяется
  тестом, а не намерением.
- Сцены и префабы создаются **кодом** (`HR.Editor.SceneBuilder`), а не правкой
  YAML. Сцена должна воспроизводиться одной командой.
- Числа живут в `GameConfig` / `DifficultyCurve` / `ThemeData`
  (`ScriptableObject`). Магических чисел в коде нет.
- Тема не меняет **ни одного** физического параметра. Проверяется хэшем.
- Визуальная толщина кольца равна физической, всегда, во всех темах.
- В игровом цикле нет `Instantiate` / `Destroy` и нет аллокаций — всё через
  пулы.
- Сеть отсутствует. `INTERNET`, `ACCESS_NETWORK_STATE` и `AD_ID` вырезаются
  кастомным манифестом; его удаление молча возвращает разрешения, поэтому
  само наличие файла — проверка в `ReleaseTool.Verify`.

## Правила работы

- **Сначала проверка, потом утверждение.** «Работает» допустимо только с
  выводом теста, бот-симуляции или лога. Свойства артефакта читаются из
  артефакта, а не из лога сборки.
- Фаза закрыта только когда пройдены её гейты. `docs/PROGRESS.md`
  обновляется после каждой задачи.
- Каждая фаза — серия коммитов и тег `phase-N-done`, Conventional Commits.
- В релизной ветке нет `TODO`, `FIXME`, закомментированного кода и отладочных
  `Debug.Log` — проверяется `tools/audit.sh`.
- Вне MVP ничего не добавляется. Идеи — в `docs/BACKLOG.md`.
- Комментарий — одна строка, без номеров задач и сборок.
- Keystore и пароли никогда не попадают в репозиторий: ключ в `~/keystores/`,
  пароли в переменных окружения.

## Раскладка

```
CLAUDE.md
docs/      CONTRACT.md DECISIONS.md PROGRESS.md BACKLOG.md GAME_DESIGN_SPEC.md
           design/ (исходники, read-only)  publishing/ (стор, privacy, арт)
tools/     preflight.sh run_tests.sh build_android.sh screenshots.sh gen_*.py
Assets/_Project/{Scripts,Config,Scenes,Editor,Tests}
```
