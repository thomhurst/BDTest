// Run with node --test scripts/Test-ReportDeletion.cjs. Executes the shipped report script.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');
const script = fs.readFileSync(path.join(__dirname, '../BDTest.Razor.Reports/wwwroot/js/BDTest.js'), 'utf8');

function browser({ status = 204, confirm = true, failure, header = 'RequestVerificationToken' } = {}) {
  const events = {};
  const requests = [];
  const confirmations = [];
  let reloads = 0;
  const snackbar = { textContent: '', classList: { remove() {}, add() {} } };
  const button = { disabled: false, dataset: { testId: "run-'\"<&", tag: "');alert(1);//", date: 'today' } };
  const form = {
    action: 'https://reports.test/host/bdtest/reports/run-849',
    dataset: { antiforgeryHeader: header },
    matches: selector => selector === 'form[data-bdtest-delete]',
    querySelector: selector => selector === 'input[type="hidden"]' ? { value: 'csrf-token' } : button
  };
  class FormData { constructor(source) { this.source = source; } }
  const context = vm.createContext({
    document: {
      readyState: 'loading', addEventListener(name, handler) { events[name] = handler; },
      querySelectorAll: () => [], getElementById: () => snackbar
    },
    window: {
      confirm(message) { confirmations.push(message); return confirm; },
      location: { reload() { reloads++; } }, addEventListener() {}
    },
    async fetch(url, options) {
      requests.push({ url, options });
      if (failure) throw failure;
      return { status };
    },
    FormData, setTimeout() {}, clearTimeout() {}
  });
  vm.runInContext(script, context);
  events.DOMContentLoaded();
  return { context, events, form, button, requests, confirmations, snackbar, reloads: () => reloads };
}

test('form submission sends one DELETE with same-origin credentials and antiforgery header', async () => {
  const ui = browser();
  let prevented = false;
  ui.events.submit({ target: ui.form, submitter: ui.button, preventDefault() { prevented = true; } });
  await new Promise(setImmediate);
  assert.ok(prevented);
  assert.equal(ui.requests.length, 1);
  const { url, options } = ui.requests[0];
  assert.equal(url, ui.form.action, 'Use the server-generated URL, including host path base');
  assert.equal(options.method, 'DELETE');
  assert.equal(options.credentials, 'same-origin');
  assert.equal(options.redirect, 'error');
  assert.equal(options.headers.RequestVerificationToken, 'csrf-token');
  assert.equal(ui.reloads(), 1);
  assert.ok(ui.confirmations[0].includes(ui.button.dataset.tag), 'Report metadata stays literal confirmation text');
});

for (const status of [200, 302, 400, 401, 403, 500]) {
  test(`HTTP ${status} shows failure and allows retry without refreshing`, async () => {
    const ui = browser({ status });
    await ui.context.deleteReport(ui.form, ui.button);
    assert.equal(ui.reloads(), 0);
    assert.equal(ui.button.disabled, false);
    assert.match(ui.snackbar.textContent, /try again/);
  });
}

test('network failure shows failure and allows retry', async () => {
  const ui = browser({ failure: new Error('connection lost') });
  await ui.context.deleteReport(ui.form, ui.button);
  assert.equal(ui.reloads(), 0);
  assert.equal(ui.button.disabled, false);
  assert.match(ui.snackbar.textContent, /try again/);
});

test('cancel and repeated submissions do not issue extra requests', async () => {
  const cancelled = browser({ confirm: false });
  await cancelled.context.deleteReport(cancelled.form, cancelled.button);
  assert.equal(cancelled.requests.length, 0);
  const ui = browser();
  await Promise.all([ui.context.deleteReport(ui.form, ui.button), ui.context.deleteReport(ui.form, ui.button)]);
  assert.equal(ui.requests.length, 1);
});

test('custom antiforgery header and form-only host settings work', async () => {
  const custom = browser({ header: 'X-CSRF-Token' });
  await custom.context.deleteReport(custom.form, custom.button);
  assert.equal(custom.requests[0].options.headers['X-CSRF-Token'], 'csrf-token');
  const formOnly = browser({ header: '' });
  await formOnly.context.deleteReport(formOnly.form, formOnly.button);
  assert.equal(formOnly.requests[0].options.body.source, formOnly.form);
  assert.equal(formOnly.reloads(), 1);
});
