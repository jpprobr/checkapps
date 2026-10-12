import { ActivatedRouteSnapshot, BaseRouteReuseStrategy } from '@angular/router';

/**
 * O padrão do Angular reaproveita o componente quando só o parâmetro muda
 * (/squads/sq-1 → /squads/sq-2). As telas leem o parâmetro do snapshot no construtor,
 * então reaproveitar deixaria a tela com os dados da squad anterior. Com o seletor de
 * squad no header essa troca virou navegação comum — aqui ela recria o componente.
 */
export class ParamAwareReuseStrategy extends BaseRouteReuseStrategy {
  override shouldReuseRoute(future: ActivatedRouteSnapshot, current: ActivatedRouteSnapshot): boolean {
    return future.routeConfig === current.routeConfig && sameParams(future.params, current.params);
  }
}

function sameParams(a: Record<string, string>, b: Record<string, string>): boolean {
  const keys = Object.keys(a);
  return keys.length === Object.keys(b).length && keys.every((key) => a[key] === b[key]);
}
