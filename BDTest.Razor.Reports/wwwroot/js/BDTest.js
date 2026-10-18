// BDTest report server behaviour. Views bind trusted actions with data attributes;
// report values are only ever read as data, never evaluated as JavaScript.

function onDomLoaded(callback) {
    if (document.readyState !== 'loading') {
        callback();
    } else {
        document.addEventListener('DOMContentLoaded', callback);
    }
}

function showSnackbar(text) {
    const snackbar = document.getElementById('snackbar');
    if (!snackbar) {
        return;
    }

    snackbar.textContent = text;
    snackbar.classList.remove('show');
    // Restart the animation when the toast is shown twice in a row.
    void snackbar.offsetWidth;
    snackbar.classList.add('show');
    clearTimeout(showSnackbar.timer);
    showSnackbar.timer = setTimeout(function () {
        snackbar.classList.remove('show');
    }, 3000);
}

function copyToClipboard(text, message) {
    const done = function () {
        showSnackbar(message || 'Copied to clipboard');
    };

    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text).then(done, function () {
            showSnackbar('Couldn’t copy. Select the text and copy it instead.');
        });
        return;
    }

    // Plain-HTTP report servers do not expose the async clipboard API.
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.select();
    try {
        document.execCommand('copy');
        done();
    } catch (e) {
        showSnackbar('Couldn’t copy. Select the text and copy it instead.');
    }
    document.body.removeChild(area);
}

function confirmDelete(element) {
    const data = element.dataset;
    let message = 'Delete this test run? This can’t be undone.\n';
    message += '\nRun ID: ' + data.testId;
    if (data.tag) {
        message += '\nTag: ' + data.tag;
    }
    if (data.environment) {
        message += '\nEnvironment: ' + data.environment;
    }
    if (data.branch) {
        message += '\nBranch: ' + data.branch;
    }
    message += '\nDate: ' + data.date;
    return window.confirm(message);
}

function setDetails(container, open) {
    for (const details of container.querySelectorAll('details[data-expandable]')) {
        details.open = open;
    }
}

function closeMenus(except) {
    for (const menu of document.querySelectorAll('details.menu[open]')) {
        if (menu !== except) {
            menu.open = false;
        }
    }
}

onDomLoaded(function () {
    document.addEventListener('click', function (event) {
        const openMenu = event.target.closest('details.menu');
        closeMenus(openMenu);

        const element = event.target.closest('[data-bdtest-action]');
        if (!element) {
            return;
        }

        switch (element.dataset.bdtestAction) {
            case 'copy-url':
                copyToClipboard(new URL(element.dataset.url, window.location.href).href, 'Link copied');
                break;
            case 'copy-text': {
                const target = document.getElementById(element.dataset.target);
                if (target) {
                    copyToClipboard(target.textContent, 'Copied');
                }
                break;
            }
            case 'toggle-wrap': {
                const target = document.getElementById(element.dataset.target);
                if (target) {
                    const wrapped = target.classList.toggle('nowrap') === false;
                    element.setAttribute('aria-pressed', wrapped ? 'true' : 'false');
                }
                break;
            }
            case 'expand-all': {
                const container = element.dataset.target ? document.getElementById(element.dataset.target) : document;
                if (container) {
                    const open = element.getAttribute('aria-expanded') !== 'true';
                    setDetails(container, open);
                    element.setAttribute('aria-expanded', open ? 'true' : 'false');
                    if (element.dataset.openText && element.dataset.closeText) {
                        element.textContent = open ? element.dataset.closeText : element.dataset.openText;
                    }
                }
                break;
            }
            case 'delete':
                if (!confirmDelete(element)) {
                    event.preventDefault();
                }
                break;
            case 'back':
                event.preventDefault();
                window.history.back();
                break;
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            closeMenus(null);
        }
    });

    // Row menus are fixed in place, so close them rather than leave them behind when the page scrolls.
    window.addEventListener('scroll', function () {
        closeMenus(null);
    }, { passive: true });

    // Filter forms apply as soon as a select changes; text inputs apply on Enter.
    document.addEventListener('change', function (event) {
        const form = event.target.closest('form[data-autosubmit]');
        if (form && event.target.matches('select, input[type="checkbox"], input[type="radio"]')) {
            document.body.classList.add('busy');
            form.requestSubmit ? form.requestSubmit() : form.submit();
        }
    });

    window.addEventListener('pagehide', function () {
        document.body.classList.remove('busy');
    });
});
