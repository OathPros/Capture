interface Reply { requestId?: string; ok: boolean; result?: unknown; error?: string; type?: string; guide?: unknown }
interface WebView { postMessage(message: unknown): void; addEventListener(type: 'message', listener: (event: MessageEvent<Reply>) => void): void }
declare global { interface Window { chrome?: { webview?: WebView } } }
const pending = new Map<string, { resolve(value: unknown): void; reject(error: Error): void; timer: ReturnType<typeof setTimeout> }>();
let flushHandler: (() => Promise<void>) | null = null;
export function onFlush(fn: () => Promise<void>) { flushHandler = fn; }
const listeners = new Set<(guide: unknown) => void>();
window.chrome?.webview?.addEventListener('message', ({ data }) => {
 if (data.type === 'editor.flush') { void (async () => { try { await flushHandler?.(); await request('app.flushComplete', { saved: true }); } catch { await request('app.flushComplete', { saved: false }); } })(); return; }
 if (data.type === 'project.changed') { listeners.forEach(fn => fn(data.guide)); return; }
 if (!data.requestId) return;
 const pendingRequest = pending.get(data.requestId); if (!pendingRequest) return;
 clearTimeout(pendingRequest.timer); pending.delete(data.requestId);
 if (data.ok) pendingRequest.resolve(data.result); else pendingRequest.reject(new Error(data.error || 'The operation failed.'));
});
export function onProjectChanged(fn: (guide: unknown) => void) { listeners.add(fn); return () => { listeners.delete(fn); }; }
export function request<T>(type: string, payload: unknown = {}): Promise<T> {
 return new Promise((resolve, reject) => {
  const webview = window.chrome?.webview;
  if (!webview) { reject(new Error('Open the Windows Capture application to record, save and export. Browser preview has no native host.')); return; }
  const requestId = crypto.randomUUID();
  const timer = setTimeout(() => { pending.delete(requestId); reject(new Error('The operation is still pending or the host did not respond. Your last saved draft is retained.')); }, 120000);
  pending.set(requestId, { resolve: value => resolve(value as T), reject, timer });
  webview.postMessage({ protocolVersion: 1, requestId, type, payload });
 });
}
