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
    for (let i = 0; i < 6; i++ ) {
        color += letters[Math.floor(Math.random() * 16)];
    }
    return color;
}

function showSnackbar(text) {
    // Get the snackbar DIV
    let snackbar = document.getElementById("snackbar");

    snackbar.textContent = text;

    // Add the "show" class to DIV
    snackbar.classList.add("show");

    // After 3 seconds, remove the show class from DIV
    setTimeout(function(){ snackbar.classList.remove("show"); }, 3000);
}

function toggleElementVisibility(element) {
    if (element.classList.contains("invisible")) {
        setElementVisible(element);
    } else {
        setElementInvisible(element);
    }
}

function setElementVisible(element) {
    element.classList.remove("invisible");
}

function setElementInvisible(element) {
    element.classList.add("invisible");
}

function toggleAlternativeText(element) {
    if(element.hasAttribute("toggle-alternative-text")) {
        let alternativeText = element.getAttribute("toggle-alternative-text");
        let currentText = element.textContent;

        element.textContent = alternativeText;
        element.setAttribute("toggle-alternative-text", currentText);
    }
}

function toggleIcon(element) {

    var toggleOnIconUrl = "/_content/BDTest.NetCore.Razor.ReportMiddleware/icons/toggle_on-24px.svg";
    var toggleOffIconUrl = "/_content/BDTest.NetCore.Razor.ReportMiddleware/icons/toggle_off-24px.svg";

    if(element.src.includes(toggleOffIconUrl)) {
        element.src = toggleOnIconUrl;
    }

    else if(element.src.includes(toggleOnIconUrl)) {
        element.src = toggleOffIconUrl;
    }
}

function collapseExpandGroupedScenarios(containerId, expand) {
    let container = document.getElementById(containerId);

    let groupedScenarios = container.getElementsByClassName("grouped-scenario-child");
    for (let groupedScenario of groupedScenarios) {
        if(expand) {
            setElementVisible(groupedScenario);
        } else {
            setElementInvisible(groupedScenario);
        }
    }

    let groupedScenariosStatuses = container.getElementsByClassName("scenario-group-status");
    for (let groupedScenarioStatus of groupedScenariosStatuses) {
        if(expand) {
            setElementInvisible(groupedScenarioStatus);
        } else {
            setElementVisible(groupedScenarioStatus);
        }
    }
}

function checkIfFilterHiddenAllStories() {
    let elements
    
    if(document.URL.includes("/stories")) {
        elements = document.querySelectorAll(".story-header");
    } else {
        elements = document.querySelectorAll(".scenario-row");
    }
    
    let anyVisible = false;
    
    for (const el of elements) {
        if(el.offsetParent !== null) {
            anyVisible = true;
            break;
        }
    }
    
    let emptyMessage = document.getElementById("wow-such-empty");
    if(!anyVisible) {
        setElementVisible(emptyMessage);
    } else {
        setElementInvisible(emptyMessage);
    }
}

// Header Burger Button for Mobile
onDomLoaded(function() {
    // Get all "navbar-burger" elements
    const $navbarBurgers = Array.prototype.slice.call(document.querySelectorAll('.navbar-burger'), 0);

    // Check if there are any navbar burgers
    if ($navbarBurgers.length > 0) {

        // Add a click event on each of them
        $navbarBurgers.forEach( el => {
            el.addEventListener('click', () => {

                // Get the target from the "data-target" attribute
                const target = el.dataset.target;
                const $target = document.getElementById(target);

                // Toggle the "is-active" class on both the "navbar-burger" and the "navbar-menu"
                el.classList.toggle('is-active');
                $target.classList.toggle('is-active');

            });
        });
    }
});

onDomLoaded(function() {
    let toggleElements = document.getElementsByClassName("toggle-hide");

    for (let toggleElement of toggleElements) {
        toggleElement.addEventListener("click", function() {
            let childElementIdToHide = toggleElement.getAttribute("element-id-to-hide");
            let childElementToHide = document.getElementById(childElementIdToHide);

            toggleElementVisibility(childElementToHide);

            toggleAlternativeText(toggleElement);

            toggleIcon(toggleElement);
        })
    }
})

onDomLoaded(function () {
    let scenarioGroupElements = document.querySelectorAll('tr[scenario-group]');

    for (let scenarioGroupElement of scenarioGroupElements) {
        scenarioGroupElement.addEventListener("click", function () {
            let scenarioGroupId = scenarioGroupElement.getAttribute("scenario-group");
            let scenarioElementsInThatGroup = document.querySelectorAll('tr[parent-scenario-group="' + scenarioGroupId + '"]');
            let scenarioGroupStatus = scenarioGroupElement.querySelector('.scenario-group-status');
            toggleElementVisibility(scenarioGroupStatus);

            Array.prototype.forEach.call (scenarioElementsInThatGroup, function (scenarioInGroup) {
                toggleElementVisibility(scenarioInGroup);
            });
        });
    }
});

function openModal(targetId) {
    closeModals();
    let $target = document.getElementById(targetId);
    document.getElementById("main-body").classList.add('is-clipped');
    $target.classList.add('is-active');
}

function closeModals() {
    document.getElementById("main-body").classList.remove('is-clipped');
    getAll('.modal').forEach(function ($el) {
        $el.classList.remove('is-active');
    });
}

function getAll(selector) {
    let parent = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : document;

    return Array.prototype.slice.call(parent.querySelectorAll(selector), 0);
}

onDomLoaded(function() {

// Modals
    let $modalButtons = getAll('.modal-link');
    let $modalCloses = getAll('.modal-background, .modal-close, .modal-card-head .delete, .modal-card-foot .button');

    if ($modalButtons.length > 0) {
        $modalButtons.forEach(function ($el) {
            $el.addEventListener('click', function () {
                let target = $el.dataset.target;
                openModal(target);
            });
        });
    }

    if ($modalCloses.length > 0) {
        $modalCloses.forEach(function ($el) {
            $el.addEventListener('click', function () {
                closeModals();
            });
        });
    }

    document.addEventListener('keydown', function (event) {
        let e = event || window.event;
        if (e.keyCode === 27) {
            closeModals();
        }
    });


// Scroll to details when clicked
    let detailsElements = document.getElementsByTagName("details");
    for (let detailsElement of detailsElements) {
        detailsElement.addEventListener('click', function () {
            if(!detailsElement.hasAttribute("open")) {
// Run on next loop after details element has expanded
                setTimeout(function() {
                    if(!isInViewport(detailsElement)) {
                        detailsElement.scrollIntoView({ behavior: "smooth" });
                    }
                }, 0);
            }
        });
    }
});