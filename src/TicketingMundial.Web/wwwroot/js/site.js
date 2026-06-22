document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((element) => {
        new bootstrap.Tooltip(element);
    });

    const phoneList = document.querySelector('[data-phone-list]');
    const addPhoneButton = document.querySelector('[data-add-phone]');
    const registerForm = document.querySelector('[data-register-form]');

    function reindexPhones() {
        if (!phoneList) {
            return;
        }

        phoneList.querySelectorAll('input').forEach((input, index) => {
            input.setAttribute('name', `Telefonos[${index}]`);
        });
    }

    function normalizePhoneValue(value) {
        return value.trim().replace(/[()\-\s]/g, '');
    }

    if (phoneList && addPhoneButton) {
        addPhoneButton.addEventListener('click', () => {
            const wrapper = document.createElement('div');
            wrapper.className = 'input-group mb-2';
            wrapper.setAttribute('data-phone-row', '');

            const input = document.createElement('input');
            input.name = 'Telefonos[0]';
            input.className = 'form-control';
            input.maxLength = 24;
            input.inputMode = 'tel';
            input.autocomplete = 'tel';

            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-outline-secondary';
            button.setAttribute('data-remove-phone', '');
            button.textContent = 'Quitar';

            wrapper.append(input, button);
            phoneList.appendChild(wrapper);
            reindexPhones();
        });

        phoneList.addEventListener('click', (event) => {
            const target = event.target;
            if (target instanceof HTMLElement && target.matches('[data-remove-phone]')) {
                const row = target.closest('.input-group');
                if (row && phoneList.querySelectorAll('.input-group').length > 1) {
                    row.remove();
                    reindexPhones();
                }
            }
        });

        phoneList.addEventListener('blur', (event) => {
            const target = event.target;
            if (target instanceof HTMLInputElement) {
                target.value = normalizePhoneValue(target.value);
            }
        }, true);
    }

    if (registerForm instanceof HTMLFormElement) {
        const countrySelect = registerForm.querySelector('[data-document-country]');
        const typeSelect = registerForm.querySelector('[data-document-type]');
        const numberInput = registerForm.querySelector('[data-document-number]');
        const helpText = registerForm.querySelector('[data-document-help]');
        let documentTypes = [];

        try {
            documentTypes = JSON.parse(registerForm.dataset.documentTypes || '[]');
        } catch {
            documentTypes = [];
        }

        const normalizeCode = (value) => (value || '').trim().toUpperCase();
        const allowsCountry = (type, country) => {
            const normalizedCountry = normalizeCode(country);
            return Array.isArray(type.paisesPermitidos) &&
                type.paisesPermitidos.some((pais) => {
                    const normalized = normalizeCode(pais);
                    return normalized === '*' || normalized === normalizedCountry;
                });
        };

        function refreshDocumentTypes() {
            if (!(countrySelect instanceof HTMLSelectElement) || !(typeSelect instanceof HTMLSelectElement)) {
                return;
            }

            const selectedType = normalizeCode(typeSelect.value);
            const country = normalizeCode(countrySelect.value);
            const allowedTypes = documentTypes.filter((type) => country && allowsCountry(type, country));

            typeSelect.replaceChildren();

            const emptyOption = document.createElement('option');
            emptyOption.value = '';
            emptyOption.textContent = allowedTypes.length === 0 ? 'No hay tipos disponibles' : 'Seleccioná un tipo';
            typeSelect.appendChild(emptyOption);

            allowedTypes.forEach((type) => {
                const option = document.createElement('option');
                option.value = type.codigo;
                option.textContent = type.nombre;
                option.selected = normalizeCode(type.codigo) === selectedType;
                typeSelect.appendChild(option);
            });

            if (allowedTypes.length > 0 && !allowedTypes.some((type) => normalizeCode(type.codigo) === selectedType)) {
                typeSelect.value = allowedTypes[0].codigo;
            }

            refreshDocumentHelp();
        }

        function refreshDocumentHelp() {
            if (!(typeSelect instanceof HTMLSelectElement)) {
                return;
            }

            const selected = normalizeCode(typeSelect.value);
            const type = documentTypes.find((item) => normalizeCode(item.codigo) === selected);
            if (numberInput instanceof HTMLInputElement) {
                numberInput.maxLength = type?.longitudMaxima || 30;
                numberInput.inputMode = type?.codigo === 'PASAPORTE' ? 'text' : 'numeric';
            }

            if (helpText instanceof HTMLElement) {
                helpText.textContent = type?.ayuda || 'Seleccioná país y tipo de documento para ver el formato esperado.';
            }
        }

        countrySelect?.addEventListener('change', refreshDocumentTypes);
        typeSelect?.addEventListener('change', refreshDocumentHelp);
        refreshDocumentTypes();
    }

    document.querySelectorAll('[data-sector-check]').forEach((checkbox) => {
        if (!(checkbox instanceof HTMLInputElement)) {
            return;
        }

        const container = checkbox.closest('div')?.parentElement;
        const priceInput = container?.querySelector('[data-sector-price]');
        if (!(priceInput instanceof HTMLInputElement)) {
            return;
        }

        function syncSectorPrice(focus) {
            priceInput.disabled = !checkbox.checked;
            priceInput.required = checkbox.checked;
            if (!checkbox.checked) {
                priceInput.value = '';
            } else if (focus) {
                priceInput.focus();
            }
        }

        checkbox.addEventListener('change', () => syncSectorPrice(true));
        syncSectorPrice(false);
    });
});
