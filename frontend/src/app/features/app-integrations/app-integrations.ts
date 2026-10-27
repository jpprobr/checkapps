import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';

import { ANALYSIS_STATUS_LABELS, CONFIDENCE_LABELS, shortSha } from '../../core/dependency-labels';
import { IntegrationsApiService } from '../../core/integrations-api.service';
import {
  INTEGRATION_STATE_LABELS,
  environmentLabel,
  integrationAddress,
  isNote,
  mechanismLabel,
  noteText,
} from '../../core/integration-labels';
import { AppIntegrations, Integration } from '../../core/models';

/**
 * Seção "Integrações" do detalhe do app (spec 014): por repositório e por seção de configuração, os endereços que ele
 * declara chamar, de qual arquivo vêm e o quanto o CheckApps confia. Só lê: a análise roda pela ferramenta
 * CheckApps.Analyze. Não diz a que app do catálogo cada endereço pertence (isso é de outra fase).
 */
@Component({
  selector: 'app-app-integrations',
  imports: [DatePipe],
  templateUrl: './app-integrations.html',
  styleUrl: './app-integrations.css',
})
export class AppIntegrationsSection {
  private readonly api = inject(IntegrationsApiService);

  readonly appId = input.required<string>();

  protected readonly data = signal<AppIntegrations | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  protected readonly confidenceLabels = CONFIDENCE_LABELS;
  protected readonly statusLabels = ANALYSIS_STATUS_LABELS;
  protected readonly stateLabels = INTEGRATION_STATE_LABELS;
  protected readonly shortSha = shortSha;
  protected readonly address = integrationAddress;
  protected readonly environment = environmentLabel;
  protected readonly mechanism = mechanismLabel;
  protected readonly isNote = isNote;
  protected readonly noteText = noteText;

  constructor() {
    effect(() => {
      const appId = this.appId();
      this.loading.set(true);
      this.failed.set(false);
      this.api.forApp(appId).subscribe({
        next: (data) => {
          this.data.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.failed.set(true);
          this.loading.set(false);
        },
      });
    });
  }

  /** A confiança só precisa de explicação quando não é alta: o motivo vem da API, em texto. */
  protected needsNote(integration: Integration): boolean {
    return integration.confidence !== 'alta' && !!integration.confidenceNote;
  }
}
