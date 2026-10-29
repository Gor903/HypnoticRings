#!/usr/bin/env python3
"""Turns the balance sweep's raw results into docs/BALANCE_REPORT.md and its charts.

Separate from the simulation on purpose: the simulation has to be the game's own code,
and the plotting has to be changeable without re-running the sweep.

Pillow only — numpy and matplotlib are not installed, and adding them would put a
network step into a build that currently has none (DECISIONS D-003).
"""
import csv
import os
import sys
from collections import defaultdict

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    sys.exit("Pillow is required: python3 -m pip install Pillow")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(ROOT, "docs/benchmarks/balance-raw.csv")
CURVE = os.path.join(ROOT, "docs/benchmarks/balance-curve.csv")
ART = os.path.join(ROOT, "docs/benchmarks")
REPORT = os.path.join(ROOT, "docs/BALANCE_REPORT.md")

# Validated with the dataviz palette checker: first three categorical slots, all-pairs,
# light surface. Worst CVD dE 9.2, worst normal-vision dE 24.0. Aqua sits under 3:1 on
# this surface, so every facet carries a visible label and the report carries the table —
# identity is never left to colour alone.
SURFACE = (252, 252, 251)
INK = (11, 11, 11)
INK_SOFT = (82, 81, 78)
GRID = (226, 226, 222)
SERIES = {"weak": (42, 120, 214), "medium": (235, 104, 52), "strong": (27, 175, 122)}
LEVELS = ["weak", "medium", "strong"]
LABEL_RU = {"weak": "слабый", "medium": "средний", "strong": "сильный"}

# The expected bands, drawn so a reader sees the target and the result together. Same numbers
# as BalanceSimulationTests, and they moved at D-059 for the reason stated there: this simulator
# asks the pre-D-058 question, so its scale no longer sets the difficulty — the play-mode gate does.
BANDS = {"weak": (8, 22), "medium": (55, 95)}


def font(size):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                 "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def read_runs():
    runs = defaultdict(list)
    with open(RAW, newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            runs[row["level"]].append(int(row["score"]))
    return runs


def read_curve():
    rows = []
    with open(CURVE, newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            rows.append({k: float(v) for k, v in row.items()})
    return rows


def stats(scores):
    ordered = sorted(scores)
    n = len(ordered)
    return {
        "n": n,
        "mean": sum(ordered) / n,
        "min": ordered[0],
        "max": ordered[-1],
        "p50": ordered[n // 2],
        "p10": ordered[int(n * 0.10)],
        "p90": ordered[int(n * 0.90)],
    }


def draw_axes(d, box, title, x_label, y_range, f_title, f_small):
    """Axis label centred under the ticks, not tucked against the right edge where it
    lands on the last tick value."""
    left, top, right, bottom = box
    d.text((left, top - 22), title, font=f_title, fill=INK)
    d.line([(left, bottom), (right, bottom)], fill=INK_SOFT, width=1)
    d.line([(left, top), (left, bottom)], fill=INK_SOFT, width=1)
    d.text(((left + right) / 2 - d.textlength(x_label, font=f_small) / 2, bottom + 24),
           x_label, font=f_small, fill=INK_SOFT)

    lo, hi = y_range
    d.text((left - 8 - d.textlength(hi, font=f_small), top - 6), hi, font=f_small, fill=INK_SOFT)
    d.text((left - 8 - d.textlength(lo, font=f_small), bottom - 8), lo, font=f_small, fill=INK_SOFT)


def histogram(runs):
    """Facets for the two skill levels that actually produce a distribution.

    The strong bot is deliberately absent: every one of its runs ends at the cap, so its
    histogram is a single bar carrying one fact, and putting it on a shared axis with the
    others stretched their range five-fold and squashed both into the left margin. One
    number belongs in a sentence, not in a chart — it is stated in the report instead.
    """
    f_title, f_small = font(15), font(12)
    plotted = [lvl for lvl in LEVELS if lvl in BANDS]

    # Room above each facet for its band caption, and below for the axis label, so
    # neither lands on its neighbour.
    width, facet_h, pad = 900, 200, 96
    height = 60 + len(plotted) * (facet_h + pad)
    image = Image.new("RGB", (width, height), SURFACE)
    d = ImageDraw.Draw(image)

    # Shared across the plotted facets so the two are directly comparable, and only just
    # wider than the data so neither sits in a corner.
    top_score = max(max(runs[lvl]) for lvl in plotted)
    top_score = int(top_score * 1.08) + 1
    bin_width = max(1, top_score // 36)
    bins = top_score // bin_width + 1

    for i, level in enumerate(plotted):
        scores = runs[level]
        counts = [0] * (bins + 1)
        for s_ in scores:
            counts[min(bins, s_ // bin_width)] += 1
        peak = max(counts) or 1

        left, right = 78, width - 28
        top = 60 + i * (facet_h + pad)
        bottom = top + facet_h

        st = stats(scores)
        d.text((left, top - 24),
               f"{LABEL_RU[level]} — среднее {st['mean']:.1f}, медиана {st['p50']}, "
               f"разброс {st['min']}\u2013{st['max']}",
               font=f_title, fill=INK)

        # The plan's expected band, behind everything, as context rather than data.
        lo, hi = BANDS[level]
        x0 = left + (right - left) * lo / top_score
        x1 = left + (right - left) * hi / top_score
        d.rectangle([x0, top, x1, bottom], fill=(238, 238, 234))
        caption = f"ожидание плана {lo}\u2013{hi}"
        d.text((x0, top - 42), caption, font=f_small, fill=INK_SOFT)
        d.line([(x0, top - 28), (x0, top)], fill=(208, 208, 203), width=1)
        d.line([(x1, top - 28), (x1, top)], fill=(208, 208, 203), width=1)

        # Horizontal guides carry their own values, which is more use than the word "runs".
        for g in range(1, 5):
            y = bottom - (bottom - top) * g / 4
            d.line([(left, y), (right, y)], fill=GRID, width=1)
            value = int(peak * g / 4)
            d.text((left - 10 - d.textlength(str(value), font=f_small), y - 7),
                   str(value), font=f_small, fill=INK_SOFT)

        d.line([(left, bottom), (right, bottom)], fill=INK_SOFT, width=1)
        d.line([(left, top), (left, bottom)], fill=INK_SOFT, width=1)

        slot = (right - left) / bins
        for b, count in enumerate(counts):
            if not count:
                continue
            x0b = left + b * slot
            x1b = x0b + slot - 2
            y0 = min(bottom - 2, bottom - (bottom - top) * count / peak)
            d.rectangle([x0b, y0, x1b, bottom - 1], fill=SERIES[level])

        step = max(5, (top_score // 6 // 5) * 5)
        for tick in range(0, top_score + 1, step):
            x = left + (right - left) * tick / top_score
            label = str(tick)
            d.text((x - d.textlength(label, font=f_small) / 2, bottom + 7),
                   label, font=f_small, fill=INK_SOFT)

        axis = "пройдено колец за прогон"
        d.text(((left + right) / 2 - d.textlength(axis, font=f_small) / 2, bottom + 26),
               axis, font=f_small, fill=INK_SOFT)

    path = os.path.join(ART, "balance-scores.png")
    image.save(path)
    return path


def curve_chart(rows):
    """Four small multiples rather than one chart with four y-axes. Compression, width,
    spin and budget share no unit; a second axis would invite a comparison that is not
    there."""
    f_title, f_small = font(15), font(12)
    panels = [
        ("compression", "скорость сжатия, u/с", (42, 120, 214)),
        ("gap_width", "ширина отверстия, u", (235, 104, 52)),
        ("auto_spin", "собственное вращение, °/с", (27, 175, 122)),
        ("time_budget", "бюджет времени, с", (74, 58, 167)),
    ]
    width, facet_h, pad = 940, 150, 76
    height = pad + len(panels) * (facet_h + pad)
    image = Image.new("RGB", (width, height), SURFACE)
    d = ImageDraw.Draw(image)

    max_ring = max(r["ring"] for r in rows)

    for i, (key, label, colour) in enumerate(panels):
        left, right = 92, width - 30
        top = pad + i * (facet_h + pad)
        bottom = top + facet_h

        values = [r[key] for r in rows]
        lo, hi = min(values), max(values)
        if hi - lo < 1e-9:
            hi = lo + 1.0

        draw_axes(d, (left, top, right, bottom), label, "пройдено колец",
                  (f"{lo:.2f}", f"{hi:.2f}"), f_title, f_small)

        for g in range(1, 4):
            y = bottom - (bottom - top) * g / 4
            d.line([(left, y), (right, y)], fill=GRID, width=1)

        points = []
        for r in rows:
            x = left + (right - left) * r["ring"] / max_ring
            y = bottom - (bottom - top) * (r[key] - lo) / (hi - lo)
            points.append((x, y))
        d.line(points, fill=colour, width=2, joint="curve")

        for tick in range(0, int(max_ring) + 1, 20):
            x = left + (right - left) * tick / max_ring
            text = str(tick)
            d.text((x - d.textlength(text, font=f_small) / 2, bottom + 6),
                   text, font=f_small, fill=INK_SOFT)

    path = os.path.join(ART, "balance-curve.png")
    image.save(path)
    return path


def write_report(runs, rows):
    by_level = {level: stats(runs[level]) for level in LEVELS}
    cap = max(max(v) for v in runs.values())

    lines = []
    lines.append("# Отчёт о балансе\n")
    lines.append("Сгенерирован `tools/balance.sh`: симуляция в `HR.Core.BalanceSimulator`, "
                 "графики — `tools/plot_balance.py`. Исходные числа лежат рядом в "
                 "`benchmarks/balance-raw.csv` и `benchmarks/balance-curve.csv`.\n")

    lines.append("## Результат\n")
    lines.append("| Уровень | Прогонов | Среднее | Медиана | 10-й проц. | 90-й проц. | Разброс | Ожидание плана | |")
    lines.append("|---|---:|---:|---:|---:|---:|---:|---:|---|")
    for level in LEVELS:
        s = by_level[level]
        band = BANDS.get(level)
        if band:
            expected = f"{band[0]}–{band[1]}"
            ok = "✅" if band[0] <= s["mean"] <= band[1] else "❌"
        else:
            expected = "не падает"
            ok = "✅" if s["min"] >= cap else "❌"
        lines.append(f"| {LABEL_RU[level]} | {s['n']} | {s['mean']:.1f} | {s['p50']} | "
                     f"{s['p10']} | {s['p90']} | {s['min']}–{s['max']} | {expected} | {ok} |")
    lines.append("")

    lines.append(f"Сильный бот упирается в потолок симуляции ({cap} колец) во всех "
                 f"{by_level['strong']['n']} прогонах. Это и есть проверка «0 непроходимых "
                 "колец»: непроходимых ситуаций в кривой нет.\n")

    lines.append("![Распределение результатов слабого и среднего ботов]"
                 "(benchmarks/balance-scores.png)\n")
    lines.append("На графике только слабый и средний: у сильного все прогоны заканчиваются "
                 "на потолке, его гистограмма — один столбик с одним фактом, а общая ось "
                 "с ним растягивала бы остальных в пять раз и вжимала в левый край. "
                 "Один факт лучше читается предложением.\n")

    lines.append("## Кривая сложности\n")
    lines.append("Четыре отдельных графика, а не один с четырьмя осями: у величин нет общей "
                 "единицы, и вторая ось приглашала бы к сравнению, которого нет.\n")
    lines.append("![Кривая сложности](benchmarks/balance-curve.png)\n")

    lines.append("| Кольцо | Сжатие, u/с | Отверстие, u | Вращение, °/с | Бюджет, с | Потолок вращения, °/с |")
    lines.append("|---:|---:|---:|---:|---:|---:|")
    for r in rows:
        if int(r["ring"]) % 10 or r["ring"] > 100:
            continue
        lines.append(f"| {int(r['ring'])} | {r['compression']:.3f} | {r['gap_width']:.2f} | "
                     f"{r['auto_spin']:.1f} | {r['time_budget']:.2f} | {r['spin_ceiling']:.1f} |")
    lines.append("")

    lines.append("## Что эти числа значат, а что нет\n")
    lines.append("**Значат.** Кривая монотонна и без скачков, непроходимых колец нет, "
                 "и три уровня мастерства упорядочены так, как должны быть.\n")
    lines.append("**Не значат.** Калибровка «среднего» и «слабого» ботов — это *определение* "
                 "того, что мы называем средним и слабым игроком, а не независимое "
                 "подтверждение сложности. Первый замер задал параметры; дальше эти числа "
                 "работают как страховка от регрессии: если правка кривой сдвинет среднее "
                 "за пределы диапазона, это будет видно. Независимая проверка здесь ровно "
                 "одна — сильный бот без шума, который не падает нигде.\n")
    lines.append("**Чего симуляция не моделирует.** Контакт: трение, уводящее шарик с "
                 "нижней точки, отскок, удары о кромку. Это проверяется отдельно — "
                 "PlayMode-тестами на настоящей физике.\n")

    with open(REPORT, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines))
    return REPORT


def main():
    if not os.path.exists(RAW):
        sys.exit(f"missing {RAW}; run the sweep first")

    runs = read_runs()
    rows = read_curve()
    os.makedirs(ART, exist_ok=True)

    print("wrote", histogram(runs))
    print("wrote", curve_chart(rows))
    print("wrote", write_report(runs, rows))


if __name__ == "__main__":
    main()
