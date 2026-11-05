import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import * as dagre from '@dagrejs/dagre';

import { MapApiService } from '../../core/map-api.service';
import { AppMap, MapNode, QualityLevel, Squad } from '../../core/models';
import { SquadsApiService } from '../../core/squads-api.service';

const NODE_W = 190;
const NODE_H = 52;
const VIEW_W = 1000;
const VIEW_H = 560;
const SQUAD_COLORS = ['#2a78d6', '#e08a1e', '#7a4fd0', '#1f9d8a', '#c2417d', '#6b8e23', '#b5651d', '#4a6fa5'];
const NEUTRAL = '#898781';

/** Uma ligação por par origem→destino no desenho; cada ambiente/arquivo base entra na lista `envs`. */
interface MergedEdge {
  key: string;
  fromId: string;
  toId: string;
  envs: string[];
  count: number;
  hosts: string[];
  selfLoop: boolean;
}

interface LaidNode {
  node: MapNode;
  x: number;
  y: number;
}

interface LaidEdge {
  edge: MergedEdge;
  path: string;
  labelX: number;
  labelY: number;
}

interface Layout {
  nodes: LaidNode[];
  edges: LaidEdge[];
  width: number;
  height: number;
}

/**
 * Tela "Mapa" (spec 020): o grafo "quem chama quem" entre os apps, por ambiente. Derivado das integrações declaradas
 * (hoje só a varredura .NET dos appsettings) e das decisões humanas da tela Integrações: nada é adivinhado, o destino sem
 * decisão é o nó "Sem app". O desenho (SVG) tem a tabela "Quem chama quem" como equivalente em texto.
 */
@Component({
  selector: 'app-map',
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './map.html',
  styleUrl: './map.css',
})
export class AppMapPage {
  private readonly api = inject(MapApiService);
  private readonly squadsApi = inject(SquadsApiService);

  protected readonly viewW = VIEW_W;
  protected readonly viewH = VIEW_H;
  protected readonly nodeW = NODE_W;
  protected readonly nodeH = NODE_H;

  protected readonly squads = signal<Squad[]>([]);
  protected readonly squadId = signal('');
  protected readonly env = signal('');
  protected readonly colorBy = signal<'squad' | 'quality'>('squad');
  protected readonly showLoops = signal(false);
  protected readonly showNoApp = signal(true);

  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly data = signal<AppMap | null>(null);
  protected readonly selectedId = signal<string | null>(null);

  protected readonly scale = signal(1);
  protected readonly tx = signal(0);
  protected readonly ty = signal(0);
  private drag: { x: number; y: number; ratio: number } | null = null;

  /** Ligações do desenho depois dos filtros; as do arquivo base ou de ambiente desconhecido valem em qualquer ambiente. */
  protected readonly edges = computed<MergedEdge[]>(() => {
    const data = this.data();
    if (!data) {
      return [];
    }
    const env = this.env();
    const merged = new Map<string, MergedEdge>();
    for (const e of data.edges) {
      if ((env && e.environmentKind !== null && e.environmentKind !== env)
        || (!this.showLoops() && e.selfLoop)
        || (!this.showNoApp() && e.toId === 'sem-app')) {
        continue;
      }
      const key = `${e.fromId}>${e.toId}`;
      const entry = merged.get(key)
        ?? { key, fromId: e.fromId, toId: e.toId, envs: [], count: 0, hosts: [], selfLoop: e.selfLoop };
      const label = e.environmentKind ?? 'base';
      if (!entry.envs.includes(label)) {
        entry.envs.push(label);
      }
      entry.count += e.count;
      for (const host of e.hosts) {
        if (!entry.hosts.includes(host)) {
          entry.hosts.push(host);
        }
      }
      merged.set(key, entry);
    }
    return [...merged.values()];
  });

  private readonly nodeById = computed(() => new Map((this.data()?.nodes ?? []).map((n) => [n.id, n])));

  /** Só entram no desenho os nós com ligação; os apps sem ligação são contados à parte. */
  protected readonly isolatedApps = computed(() => {
    const used = new Set(this.edges().flatMap((e) => [e.fromId, e.toId]));
    return (this.data()?.nodes ?? []).filter((n) => n.kind === 'app' && !used.has(n.id));
  });

  protected readonly layout = computed<Layout>(() => this.computeLayout());

  protected readonly rows = computed(() =>
    [...this.edges()].sort((a, b) =>
      this.label(a.fromId).localeCompare(this.label(b.fromId)) || this.label(a.toId).localeCompare(this.label(b.toId))));

  protected readonly selected = computed(() => {
    const id = this.selectedId();
    return id ? (this.nodeById().get(id) ?? null) : null;
  });

  protected readonly selectedOut = computed(() => this.edges().filter((e) => e.fromId === this.selectedId() && !e.selfLoop));
  protected readonly selectedIn = computed(() => this.edges().filter((e) => e.toId === this.selectedId() && !e.selfLoop));

  /** Legenda em texto: nunca só cor. */
  protected readonly legend = computed(() => {
    if (this.colorBy() === 'quality') {
      return (['A', 'B', 'C', 'D', 'E'] as QualityLevel[]).map((l) => ({ name: `Nível ${l}`, color: this.qualityColor(l) }))
        .concat([{ name: 'Sem nível', color: NEUTRAL }]);
    }
    return this.squadList().map((s) => ({ name: s.name, color: this.squadColor(s.id) }));
  });

  private readonly squadList = computed(() => {
    const seen = new Map<string, string>();
    for (const n of this.data()?.nodes ?? []) {
      if (n.squadId) {
        seen.set(n.squadId, n.squadName ?? n.squadId);
      }
    }
    return [...seen].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });

  constructor() {
    this.squadsApi.list().subscribe((squads) => this.squads.set(squads));
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.selectedId.set(null);
    this.api.map(this.squadId() || null).subscribe({
      next: (data) => {
        this.data.set(data);
        this.loading.set(false);
        this.fit();
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  protected label(id: string): string {
    return this.nodeById().get(id)?.label ?? id;
  }

  protected kindLabel(node: MapNode): string {
    if (node.kind === 'externo') {
      return 'serviço externo';
    }
    if (node.kind === 'sem-app') {
      return 'hosts sem decisão';
    }
    return `${node.squadName ?? 'sem squad'} · nível ${node.qualityLevel ?? '—'}`;
  }

  protected shorten(text: string, max = 26): string {
    return text.length > max ? `${text.slice(0, max - 1)}…` : text;
  }

  protected color(node: MapNode): string {
    if (node.kind !== 'app') {
      return NEUTRAL;
    }
    return this.colorBy() === 'quality'
      ? this.qualityColor(node.qualityLevel)
      : (node.squadId ? this.squadColor(node.squadId) : NEUTRAL);
  }

  private squadColor(id: string): string {
    const index = this.squadList().findIndex((s) => s.id === id);
    return SQUAD_COLORS[(index < 0 ? 0 : index) % SQUAD_COLORS.length];
  }

  private qualityColor(level: QualityLevel | null): string {
    return level ? `var(--quality-${level.toLowerCase()})` : NEUTRAL;
  }

  protected envText(edge: MergedEdge): string {
    return edge.envs.join(' · ');
  }

  protected highlighted(edge: MergedEdge): boolean {
    const id = this.selectedId();
    return id === null || edge.fromId === id || edge.toId === id;
  }

  protected toggle(id: string): void {
    this.selectedId.set(this.selectedId() === id ? null : id);
  }

  // ---- Zoom e arrasto: botões (teclado), roda e arrasto do fundo -----------------------------------------------------

  protected zoom(factor: number): void {
    const next = Math.min(3, Math.max(0.2, this.scale() * factor));
    const ratio = next / this.scale();
    // Mantém o centro da janela no mesmo ponto do desenho.
    this.tx.set(VIEW_W / 2 - (VIEW_W / 2 - this.tx()) * ratio);
    this.ty.set(VIEW_H / 2 - (VIEW_H / 2 - this.ty()) * ratio);
    this.scale.set(next);
  }

  protected onWheel(event: WheelEvent): void {
    event.preventDefault();
    this.zoom(event.deltaY < 0 ? 1.15 : 1 / 1.15);
  }

  protected fit(): void {
    const { width, height } = this.layout();
    const scale = Math.min(1, VIEW_W / (width + 40), VIEW_H / (height + 40));
    this.scale.set(scale);
    this.tx.set((VIEW_W - width * scale) / 2);
    this.ty.set((VIEW_H - height * scale) / 2);
  }

  protected startDrag(event: PointerEvent): void {
    const svg = event.currentTarget as SVGSVGElement;
    this.drag = { x: event.clientX, y: event.clientY, ratio: VIEW_W / svg.getBoundingClientRect().width };
    svg.setPointerCapture(event.pointerId);
  }

  protected moveDrag(event: PointerEvent): void {
    if (!this.drag) {
      return;
    }
    this.tx.set(this.tx() + (event.clientX - this.drag.x) * this.drag.ratio);
    this.ty.set(this.ty() + (event.clientY - this.drag.y) * this.drag.ratio);
    this.drag = { ...this.drag, x: event.clientX, y: event.clientY };
  }

  protected endDrag(): void {
    this.drag = null;
  }

  /** Ao mudar um filtro o desenho muda de tamanho: reajusta a janela. */
  protected refit(): void {
    queueMicrotask(() => this.fit());
  }

  private computeLayout(): Layout {
    const edges = this.edges();
    const ids = new Set(edges.flatMap((e) => [e.fromId, e.toId]));
    const nodes = [...ids].map((id) => this.nodeById().get(id)).filter((n): n is MapNode => !!n);
    if (nodes.length === 0) {
      return { nodes: [], edges: [], width: 0, height: 0 };
    }

    const graph = new dagre.graphlib.Graph();
    graph.setGraph({ rankdir: 'LR', nodesep: 24, ranksep: 90, marginx: 20, marginy: 20 });
    graph.setDefaultEdgeLabel(() => ({}));
    for (const n of nodes) {
      graph.setNode(n.id, { width: NODE_W, height: NODE_H });
    }
    for (const e of edges.filter((x) => !x.selfLoop)) {
      graph.setEdge(e.fromId, e.toId);
    }
    dagre.layout(graph);

    const laidNodes = nodes.map((node) => {
      const p = graph.node(node.id);
      return { node, x: p.x, y: p.y };
    });
    const position = new Map(laidNodes.map((n) => [n.node.id, n]));
    const laidEdges: LaidEdge[] = edges.map((edge) => {
      if (edge.selfLoop) {
        const p = position.get(edge.fromId)!;
        const x = p.x + NODE_W / 2;
        return {
          edge,
          path: `M ${x} ${p.y - 8} C ${x + 38} ${p.y - 30}, ${x + 38} ${p.y + 30}, ${x} ${p.y + 8}`,
          labelX: x + 40,
          labelY: p.y,
        };
      }
      const points: { x: number; y: number }[] = graph.edge(edge.fromId, edge.toId).points;
      const mid = points[Math.floor(points.length / 2)];
      return { edge, path: points.map((pt: { x: number; y: number }, i: number) => `${i === 0 ? 'M' : 'L'} ${pt.x} ${pt.y}`).join(' '), labelX: mid.x, labelY: mid.y };
    });

    const info = graph.graph();
    return { nodes: laidNodes, edges: laidEdges, width: (info.width ?? 0) + 60, height: info.height ?? 0 };
  }
}
