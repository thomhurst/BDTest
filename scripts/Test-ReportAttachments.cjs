// Execute the shipped report script without browser dependencies.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');
const script = fs.readFileSync(path.join(__dirname, '../BDTest.Razor.Reports/wwwroot/js/BDTest.js'), 'utf8');

function browser({ cachedFailure = false } = {}) {
    const events = {};
    const unavailable = { hidden: true };
    const caption = { textContent: '' };
    const previewUnavailable = { hidden: true };
    const container = { querySelector: () => unavailable };
    const image = {
        src: '/host/bdtest/report/report-id/attachments/image-id', alt: '<img onerror="alert(1)">',
        complete: cachedFailure, naturalWidth: cachedFailure ? 0 : 20, hidden: false,
        matches: selector => selector === 'img[data-attachment-image]',
        closest: selector => selector === '.attachment-preview' ? button : container
    };
    const button = { hidden: false, dataset: { bdtestAction: 'preview-attachment' }, querySelector: () => image };
    const preview = {
        matches: image.matches,
        closest: selector => selector === '.attachment-preview' ? null : dialog
    };
    const dialog = {
        open: false,
        showModal() { this.open = true; }, close() { this.open = false; },
        querySelector(selector) {
            return selector === 'img' ? preview : selector === '#attachment-caption' ? caption : previewUnavailable;
        }
    };
    const context = vm.createContext({
        document: {
            readyState: 'loading', addEventListener(name, handler, capture) { events[name] = { handler, capture }; },
            getElementById: () => dialog,
            querySelectorAll: selector => selector === 'img[data-attachment-image][src]' ? [image] : []
        },
        window: { addEventListener() {} }
    });
    vm.runInContext(script, context);
    events.DOMContentLoaded.handler();
    const click = control => events.click.handler({ target: { closest: selector => selector === '[data-bdtest-action]' ? control : null } });
    return { events, image, button, preview, dialog, caption, unavailable, previewUnavailable, click };
}

test('thumbnail opens a modal with literal caption text and closes through its button', () => {
    const ui = browser();
    ui.click(ui.button);
    assert.equal(ui.dialog.open, true);
    assert.equal(ui.preview.src, ui.image.src);
    assert.equal(ui.caption.textContent, ui.image.alt);
    assert.equal(ui.preview.alt, ui.image.alt);
    assert.equal(ui.preview.hidden, false);
    ui.click({ dataset: { bdtestAction: 'close-attachment' } });
    assert.equal(ui.dialog.open, false);
});

test('failed thumbnail reveals a fallback without removing the attachment download', () => {
    const ui = browser();
    assert.equal(ui.events.error.capture, true, 'Image error events do not bubble');
    ui.events.error.handler({ target: ui.image });
    assert.equal(ui.image.hidden, true);
    assert.equal(ui.button.hidden, true);
    assert.equal(ui.unavailable.hidden, false);
});

test('cached failures that precede script initialization also reveal the fallback', () => {
    const ui = browser({ cachedFailure: true });
    assert.equal(ui.button.hidden, true);
    assert.equal(ui.unavailable.hidden, false);
});

test('a lightbox load failure is recoverable when opening another image', () => {
    const ui = browser();
    ui.click(ui.button);
    ui.events.error.handler({ target: ui.preview });
    assert.equal(ui.preview.hidden, true);
    assert.equal(ui.previewUnavailable.hidden, false);
    ui.click({ dataset: { bdtestAction: 'close-attachment' } });
    ui.image.src = '/host/bdtest/report/report-id/attachments/other-image';
    ui.click(ui.button);
    assert.equal(ui.preview.hidden, false);
    assert.equal(ui.previewUnavailable.hidden, true);
    assert.equal(ui.preview.src, ui.image.src);
});
