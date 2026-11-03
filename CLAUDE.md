# Hypnotic Rings — правила проекта

Бесконечная аркада на 2D-физике: концентрические кольца сжимаются к центру,
игрок вращает их пальцем, шарик проваливается сквозь отверстия наружу.
Unity 6000.6.4f1, Android, портрет, оффлайн, бесплатно, без рекламы и покупок.

## Что читать первым после потери контекста

1. этот файл
2. `docs/PROGRESS.md` — где остановились
3. `docs/HYPNOTIC_RINGS_GAMEPLAY_AND_VISUALS.md` — что строим сейчас
4. `docs/DECISIONS.md` — что уже решено и почему
5. `docs/CONTRACT.md` — как устроен v1, от которого идём

## Приоритет документов при конфликте

Идёт работа над **v2**. Источник поведения и чисел —
`docs/HYPNOTIC_RINGS_GAMEPLAY_AND_VISUALS.md`, для режима «хардкор» —
`docs/HYPNOTIC_RINGS_HARDCORE_PLAN.md`. Оба правятся автором, ссылаются друг на
друга якорями `[G-xx]` / `[V-xx]` / `[H-xx]` и при конфликте главнее всего
остального (D-061).

`docs/design/`, `docs/GAME_DESIGN_SPEC.md` и `docs/CONTRACT.md` описывают
**v1** — он собран и зафиксирован тегом `v1-mvp`. Они не редактируются и
остаются тем, с чем сравнивают. Отступление от планов v2 — записью в
`docs/DECISIONS.md` и только с доказательством (тест, бот-симуляция, замер).

## Команды

```sh
tools/preflight.sh          # окружение: Unity, Android-модуль, память, диск
tools/run_tests.sh cheap    # только EditMode, ~1 мин — после каждой пачки правок
tools/run_tests.sh full     # оба набора с выносливостью — граница слайса, merge, сборка
tools/screenshots.sh        # все кадры: темы, экраны, скины, контактные листы
tools/build_android.sh      # подписанный .aab (нужны переменные окружения)
tools/verify_artifact.sh    # что в артефакте на самом деле; .aab и .apk
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
- Каждый слайс v2 — ветка `v2-N-<имя>`, серия коммитов и merge в `experiments`.
  Conventional Commits. Неудачный слайс выбрасывается удалением ветки.
- В релизной ветке нет `TODO`, `FIXME`, закомментированного кода и отладочных
  `Debug.Log` — проверяется `tools/audit.sh`.
- Вне двух планов v2 ничего не добавляется. Идеи — в `docs/BACKLOG.md`.
- Комментарий — одна строка, без номеров задач и сборок.
- Keystore и пароли никогда не попадают в репозиторий: ключ в `~/keystores/`,
  пароли в переменных окружения.
- **В репозитории нет выходов.** Сборки, снятые кадры и арт для стора живут в
  `~/Desktop/game-builds/HypnoticRings/{builds,screenshots,store-art}`. В
  проекте — только то, из чего они делаются: код, тесты, конфиг, тексты.
  Семьдесят шесть PNG, переписываемых каждой съёмкой, довели `.git` до
  шестидесяти мегабайт ради файлов, восстановимых одной командой за две минуты,
  и утопили диф слайса в двоичном шуме. Путь знает одно место —
  `HR.Core.ArtifactPaths`, и его повторяют `tools/_common.sh` и
  `check-listing.py`; `HR_ARTIFACTS` перекрывает.

## Раскладка

```
CLAUDE.md
~/Desktop/game-builds/HypnoticRings/   builds/ screenshots/ store-art/  (вне репозитория)

docs/      HYPNOTIC_RINGS_GAMEPLAY_AND_VISUALS.md  HYPNOTIC_RINGS_HARDCORE_PLAN.md
           CONTRACT.md DECISIONS.md PROGRESS.md BACKLOG.md GAME_DESIGN_SPEC.md
           design/ (исходники, read-only)  publishing/ (стор, privacy, арт)
tools/     preflight.sh run_tests.sh build_android.sh screenshots.sh
           verify_artifact.sh verify_apk.sh contact_sheet.py gen_*.py
Assets/_Project/{Scripts,Config,Scenes,Editor,Tests}
```
