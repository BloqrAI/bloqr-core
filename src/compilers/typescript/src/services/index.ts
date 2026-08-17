/**
 * @module services
 * High-level service layer for the compiler-core engine.
 *
 * Exports:
 * - {@link FilterService} – wraps source downloading in a service-oriented
 *   interface.
 *
 * NOTE: bloqr-compiler's AnalyticsService, PipelineService, neonApiService,
 * and ASTViewerService are commercial/Cloudflare-specific and are not part
 * of this core engine.
 */
export { FilterService } from './FilterService.ts';
