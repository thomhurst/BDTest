function copyToClipboard(text) {
    navigator.clipboard.writeText(text);

    showSnackbar("Copied to clipboard!");
}

function isInViewport(elem) {
    let bounding = elem.getBoundingClientRect();
    return (
        bounding.top >= 0 &&
        bounding.left >= 0 &&
        bounding.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
        bounding.right <= (window.innerWidth || document.documentElement.clientWidth)
    );
}

function getRandomColor() {
    let letters = '0123456789ABCDEF'.split('');
    let color = '#';
    for (let i = 0; i < 6; i++) {
        color += letters[Math.floor(Math.random() * 16)];
    }
    return color;
}

function trimEnd(value, stringToTrim) {
    if(value.substring(value.length - 1, value.length) === stringToTrim) {
        return value.substring(0, value.length - 1);
    }
    
    return value;
}

function getUrlWithAppendedParameter(paramName, paramValue) {
    let url = trimEnd(window.location.href, "#");
    if (url.indexOf(paramName + "=") >= 0) {
        let prefix = url.substring(0, url.indexOf(paramName));
        let suffix = url.substring(url.indexOf(paramName));
        suffix = suffix.substring(suffix.indexOf("=") + 1);
        suffix = (suffix.indexOf("&") >= 0) ? suffix.substring(suffix.indexOf("&")) : "";
        url = prefix + paramName + "=" + paramValue + suffix;
    } else {
        if (url.indexOf("?") < 0)
            url += "?" + paramName + "=" + paramValue;
        else
            url += "&" + paramName + "=" + paramValue;
    }

    return url;
}

function showSnackbar(text) {
    // Get the snackbar DIV
    let snackbar = document.getElementById("snackbar");

    snackbar.textContent = text;

    // Add the "show" class to DIV
    snackbar.classList.add("show");

    // After 3 seconds, remove the show class from DIV
    setTimeout(function() {
        snackbar.classList.remove("show");
    }, 3000);
}

function toggleAlternativeText(element) {
    if (element.hasAttribute("toggle-alternative-text")) {
        let alternativeText = element.getAttribute("toggle-alternative-text");
        let currentText = element.textContent;

        element.textContent = alternativeText;
        element.setAttribute("toggle-alternative-text", currentText);
    }
}

function getAll(selector) {
    let parent = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : document;

    return Array.prototype.slice.call(parent.querySelectorAll(selector), 0);
}

function setCookie(name, value, days) {
    var expires = "";
    if (days) {
        var date = new Date();
        date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
        expires = "; expires=" + date.toUTCString();
    }
    document.cookie = name + "=" + (value || "") + expires + "; path=/";
}

function getCookie(name) {
    var nameEQ = name + "=";
    var ca = document.cookie.split(';');
    for (var i = 0; i < ca.length; i++) {
        var c = ca[i];
        while (c.charAt(0) == ' ') c = c.substring(1, c.length);
        if (c.indexOf(nameEQ) == 0) return c.substring(nameEQ.length, c.length);
    }
    return null;
}

onDomLoaded(function () {
    let headerCheckbox = document.getElementById("checkbox-header");
    
    if(headerCheckbox == null) {
        return
    }
    
    headerCheckbox.addEventListener('click', function() {
        if(headerCheckbox.checked === false) {
            for (let checkbox of document.getElementsByClassName("checkbox-child")) {
                checkbox.checked = false;
            }
        } else {
            for (let checkbox of document.getElementsByClassName("checkbox-child")) {
                if(!checkbox.parentElement.parentElement.classList.contains("invisible")) {
                    checkbox.checked = true;
                }
            }
        }
    }) 
});

function getDatePicker() {
    return new Litepicker({
        element: document.getElementById("date-time-input-button"),
        elementEnd: document.getElementById("date-time-input-end"),
        position: 'bottom',
        allowRepick: true,
        singleMode: false,
        autoApply: true,
        plugins: ['ranges'],
        setup: (picker) => {
            picker.on('selected', (startDate, endDate) => {
                window.location.href = getUrlWithAppendedParameter("datetimerange", `${startDate.dateInstance.toISOString()}..${new Date(endDate.dateInstance.setUTCHours(23, 59, 59, 999)).toISOString()}`)
            });
        }
    })
}

function post(path, params, method='post') {

    // The rest of this code assumes you are not using a library.
    // It can be made less verbose if you use one.
    const form = document.createElement('form');
    form.method = method;
    form.action = path;

    for (const key in params) {
        if (params.hasOwnProperty(key)) {
            const hiddenField = document.createElement('input');
            hiddenField.type = 'hidden';
            hiddenField.name = key;
            hiddenField.value = params[key];

            form.appendChild(hiddenField);
        }
    }

    document.body.appendChild(form);
    form.submit();
}

function disableButtonAndSpin(element) {
    spinnerCursor();
    element.disabled = true;
    element.innerHTML = "<span class=\"spinner-border spinner-border-sm\" role=\"status\" aria-hidden=\"true\"></span>\n" +
        "<span class=\"visually-hidden\">Loading...</span";
}

function spinnerCursor() {
    document.body.style.cursor = 'wait';
}

const toggleActions = {
    TOGGLE: "toggle",
    COLLAPSE: "collapse",
    EXPAND: "expand"
}

function expandScenarioGroups(toggleAction) {
    let scenarioGroupRows = document.getElementsByClassName("scenario-group");
    for (let scenarioGroupRow of scenarioGroupRows) {
        expandScenarioGroup(scenarioGroupRow, toggleAction)
    }
}

function expandScenarioGroup(scenarioGroupRow, toggleAction) {
    let scenarioGroup = scenarioGroupRow.getAttribute("scenario-group");
    let childRows = document.querySelectorAll('[parent-scenario-group="' + scenarioGroup + '"]')
    for (let childRow of childRows) {
        switch (toggleAction) {
            case toggleActions.TOGGLE:
                childRow.classList.toggle("show");
                break;
            case toggleActions.EXPAND:
                childRow.classList.add("show");
                break;
            case toggleActions.COLLAPSE:
                childRow.classList.remove("show");
                break;
        }
    }
}

function setElementVisibility(element, toggleAction) {
    switch (toggleAction) {
        case toggleActions.TOGGLE:
            element.classList.toggle("invisibleElement");
            break;
        case toggleActions.EXPAND:
            element.classList.remove("invisibleElement");
            break;
        case toggleActions.COLLAPSE:
            element.classList.add("invisibleElement");
            break;
    }
}

function toggleElementVisibility(element) {
    setElementVisibility(element, toggleActions.TOGGLE);
}

function showElement(element) {
    setElementVisibility(element, toggleActions.EXPAND);
}

function hideElement(element) {
    setElementVisibility(element, toggleActions.COLLAPSE);
}

// Bind trusted functions to data attributes; report values are never evaluated as JavaScript.
onDomLoaded(function () {
    for (const element of document.querySelectorAll('[data-bdtest-action]')) {
        element.addEventListener('click', function (event) {
            if (element.tagName === 'A' && element.getAttribute('href') === '#') {
                event.preventDefault();
            }

            switch (element.dataset.bdtestAction) {
                case 'spin':
                    disableButtonAndSpin(element);
                    break;
                case 'toggle-menu':
                    element.classList.toggle('active');
                    break;
                case 'back':
                    window.history.back();
                    break;
                case 'copy-url':
                    copyToClipboard(new URL(element.dataset.url, window.location.href).href);
                    break;
                case 'navigate':
                    if (element.dataset.url) window.location.href = element.dataset.url;
                    break;
                case 'filter':
                    window.location.href = getUrlWithAppendedParameter(element.dataset.parameter, element.dataset.value);
                    break;
                case 'order':
                    orderBy(element.dataset.value);
                    break;
                case 'status':
                    filterByStatus(element.dataset.value);
                    break;
                case 'modal':
                    openModal(element.dataset.value);
                    break;
                case 'toggle-group':
                    expandScenarioGroup(element, toggleActions.TOGGLE);
                    break;
                case 'groups':
                    expandScenarioGroups(element.dataset.mode);
                    break;
                case 'stories':
                    setVisibilityOfStories(element.dataset.mode);
                    break;
                case 'story-groups':
                    toggleSpecificScenarioGroup(element, element.dataset.mode);
                    break;
                case 'toggle-element':
                    toggleElementVisibility(document.getElementById(element.dataset.target));
                    toggleAlternativeText(element);
                    break;
                case 'toggle-step':
                    document.getElementById(element.dataset.target).classList.toggle('dropend');
                    break;
                case 'expand-steps':
                    expandAllSteps();
                    break;
                case 'collapse-steps':
                    collapseAllSteps();
                    break;
                case 'column':
                    // Let native input/label clicks toggle exactly once.
                    if (!event.target.closest('input, label')) {
                        changeCheckboxValueAndFireEvent(element.querySelector('input'));
                    }
                    break;
                case 'delete':
                    if (!confirmDelete(element.dataset.testId, element.dataset.tag,
                        element.dataset.environment, element.dataset.branch, element.dataset.date)) {
                        event.preventDefault();
                    }
                    break;
            }
        });
    }

    for (const checkbox of document.querySelectorAll('[data-bdtest-change="column"]')) {
        checkbox.addEventListener('change', function () {
            toggleColumnVisibility(checkbox);
        });
    }
});
