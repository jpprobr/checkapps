import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

import { QualityLevel, QualitySnapshot } from '../../core/models';

const LEVEL_ORDER: QualityLevel[] = ['A', 'B', 'C', 'D', 'E'];
const COLOR_BY_LEVEL: Record<QualityLevel, string> = {
  A: 'var(--quality-a)',
  B: 'var(--quality-b)',
  C: 'var(--quality-c)',
  D: 'var(--quality-d)',
  E: 'var(--quality-e)',
};

const WIDTH = 640;
const HEIGHT = 220;
const MARGIN = { top: 16, right: 16, bottom: 32, left: 32 };

interface ChartPoint {
  x: number;
  y: number;
  level: QualityLevel;
  color: string;
  collectedAt: string;
}

@Component({
  selector: 'app-quality-history-chart',
  imports: [DatePipe],
  templateUrl: './quality-history-chart.html',
  styleUrl: './quality-history-chart.css',
})
export class QualityHistoryChart {
  readonly snapshots = input.required<QualitySnapshot[]>();

  protected readonly levelOrder = LEVEL_ORDER;
  protected readonly colorByLevel = COLOR_BY_LEVEL;
  protected readonly width = WIDTH;
  protected readonly height = HEIGHT;

  private readonly plotWidth = WIDTH - MARGIN.left - MARGIN.right;
  private readonly plotHeight = HEIGHT - MARGIN.top - MARGIN.bottom;

  protected readonly points = computed<ChartPoint[]>(() => {
    const ordered = [...this.snapshots()].sort(
      (a, b) => new Date(a.collectedAt).getTime() - new Date(b.collectedAt).getTime(),
    );

    return ordered.map((snapshot, index) => {
      const x = ordered.length === 1 ? MARGIN.left + this.plotWidth / 2 : MARGIN.left + (index / (ordered.length - 1)) * this.plotWidth;
      const levelIndex = LEVEL_ORDER.indexOf(snapshot.qualityLevel);
      const y = MARGIN.top + (levelIndex / (LEVEL_ORDER.length - 1)) * this.plotHeight;
      return { x, y, level: snapshot.qualityLevel, color: COLOR_BY_LEVEL[snapshot.qualityLevel], collectedAt: snapshot.collectedAt };
    });
  });

  protected readonly linePath = computed(() =>
    this.points()
      .map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`)
      .join(' '),
  );

  protected readonly rowY = (level: QualityLevel): number => {
    const levelIndex = LEVEL_ORDER.indexOf(level);
    return MARGIN.top + (levelIndex / (LEVEL_ORDER.length - 1)) * this.plotHeight;
  };
}
