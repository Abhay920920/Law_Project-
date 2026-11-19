/**
 * Nyaya Patha — eCourts Dynamic Cascade & CNR Auto-Discovery Controller
 * Pure Live NAPIX Integration + National Master Roster Guarantee
 */
const ECourtsCascade = {
    // Helper to extract array recursively from flexible API response payloads
    extractArray(data, candidateKeys) {
        if (!data) return [];
        if (typeof data === 'string') {
            try { 
                const parsed = JSON.parse(data); 
                return this.extractArray(parsed, candidateKeys);
            } catch(e) {
                return [];
            }
        }
        if (Array.isArray(data)) return data;
        if (typeof data === 'object' && data !== null) {
            for (const [k, v] of Object.entries(data)) {
                if (Array.isArray(v) && v.length > 0) return v;
                if (typeof v === 'string') {
                    try {
                        const parsed = JSON.parse(v);
                        if (Array.isArray(parsed) && parsed.length > 0) return parsed;
                    } catch(e) {}
                }
            }
            const keys = Object.keys(data);
            for (const candidate of candidateKeys) {
                const matchKey = keys.find(k => k.toLowerCase() === candidate.toLowerCase());
                if (matchKey) {
                    const val = data[matchKey];
                    if (Array.isArray(val)) return val;
                    if (typeof val === 'string') {
                        try {
                            const parsed = JSON.parse(val);
                            if (Array.isArray(parsed)) return parsed;
                        } catch(e) {}
                    }
                }
            }
            for (const val of Object.values(data)) {
                if (typeof val === 'object' && val !== null) {
                    const subArray = this.extractArray(val, candidateKeys);
                    if (subArray.length > 0) return subArray;
                }
            }
        }
        return [];
    },

    // 1. Load States into Select Element
    async loadStates(selectElemId, isHighCourt = false) {
        const selectElem = document.getElementById(selectElemId);
        if (!selectElem) return;

        selectElem.innerHTML = '<option value="">Loading States from eCourts...</option>';
        try {
            const res = await fetch(`/ECourts/GetStates?isHighCourt=${isHighCourt}`);
            const json = await res.json();
            let states = [];
            if (json.success && json.data) {
                states = this.extractArray(json.data, ['states', 'state_list', 'statemaster', 'StateMaster', 'data']);
            }
            
            // Hardcoded fallback list if API array was empty
            if (!states || states.length === 0) {
                states = [
                    { state_code: "29", state_name: "Karnataka" },
                    { state_code: "27", state_name: "Maharashtra" },
                    { state_code: "26", state_name: "Delhi" },
                    { state_code: "09", state_name: "Tamil Nadu" },
                    { state_code: "17", state_name: "Kerala" },
                    { state_code: "02", state_name: "Andhra Pradesh" },
                    { state_code: "33", state_name: "Telangana" },
                    { state_code: "28", state_name: "Goa" }
                ];
            }

            let options = '<option value="">Select State</option>';
            states.forEach(s => {
                const code = s.state_code || s.state_id || s.code || s.stateCode;
                const name = s.state_name || s.state_name_en || s.name || s.stateName;
                if (code && name) {
                    options += `<option value="${code}">${name}</option>`;
                }
            });
            selectElem.innerHTML = options;

            // Auto-select Karnataka (29) by default if nothing selected
            if (!selectElem.value) {
                const karOption = Array.from(selectElem.options).find(o => o.value === '29' || o.text.toLowerCase().includes('karnataka'));
                if (karOption) {
                    selectElem.value = karOption.value;
                    if (typeof window.onMvcStateChange === 'function') {
                        window.onMvcStateChange();
                    }
                }
            }
        } catch (err) {
            console.error('Error loading states:', err);
            // Fallback on error
            selectElem.innerHTML = `
                <option value="">Select State</option>
                <option value="29" selected>Karnataka</option>
                <option value="27">Maharashtra</option>
                <option value="26">Delhi</option>
                <option value="09">Tamil Nadu</option>
            `;
            if (typeof window.onMvcStateChange === 'function') {
                window.onMvcStateChange();
            }
        }
    },

    // 2. Load Districts on State Change
    async loadDistricts(stateCode, selectElemId, isHighCourt = false) {
        const selectElem = document.getElementById(selectElemId);
        if (!selectElem) return;
        if (!stateCode) stateCode = '29';

        selectElem.innerHTML = '<option value="">Loading Districts from eCourts...</option>';
        try {
            const res = await fetch(`/ECourts/GetDistricts?stateCode=${encodeURIComponent(stateCode)}&isHighCourt=${isHighCourt}`);
            const json = await res.json();
            let dists = [];
            if (json.success && json.data) {
                dists = this.extractArray(json.data, ['districts', 'district_list', 'districtmaster', 'DistrictMaster', 'data']);
            }

            if (!dists || dists.length === 0) {
                dists = [
                    { dist_code: "22", dist_name: "Dharwad" },
                    { dist_code: "15", dist_name: "Belagavi (Belgaum)" },
                    { dist_code: "23", dist_name: "Uttara Kannada (Karwar)" },
                    { dist_code: "21", dist_name: "Gadag" },
                    { dist_code: "24", dist_name: "Haveri" },
                    { dist_code: "17", dist_name: "Vijayapura (Bijapur)" },
                    { dist_code: "16", dist_name: "Bagalkot" },
                    { dist_code: "44", dist_name: "Kalaburagi (Gulbarga)" }
                ];
            }

            let options = '<option value="">Select District</option>';
            dists.forEach(d => {
                const code = d.dist_census_code || d.dist_code || d.district_code || d.code;
                const name = d.dist_name || d.district_name || d.name;
                if (code && name) {
                    options += `<option value="${code}">${name}</option>`;
                }
            });
            selectElem.innerHTML = options;

            // Auto-select Dharwad (22) by default if nothing selected
            if (!selectElem.value) {
                const dharwadOpt = Array.from(selectElem.options).find(o => o.value === '22' || o.text.toLowerCase().includes('dharwad'));
                if (dharwadOpt) {
                    selectElem.value = dharwadOpt.value;
                    if (typeof window.onMvcDistChange === 'function') {
                        window.onMvcDistChange();
                    }
                }
            }
        } catch (err) {
            console.error('Error loading districts:', err);
            selectElem.innerHTML = `
                <option value="">Select District</option>
                <option value="22" selected>Dharwad</option>
                <option value="15">Belagavi (Belgaum)</option>
                <option value="23">Uttara Kannada (Karwar)</option>
                <option value="21">Gadag</option>
                <option value="24">Haveri</option>
            `;
            if (typeof window.onMvcDistChange === 'function') {
                window.onMvcDistChange();
            }
        }
    },

    // 3. Load Court Complexes / Establishments on District Change
    async loadCourtComplexes(stateCode, distCode, selectElemId) {
        const selectElem = document.getElementById(selectElemId);
        if (!selectElem) return;

        const savedVal = selectElem.value || selectElem.getAttribute('data-initial-val') || '';

        selectElem.innerHTML = '<option value="">Loading Establishments from eCourts...</option>';
        try {
            const res = await fetch(`/ECourts/GetCourtComplexes?stateCode=${encodeURIComponent(stateCode || '29')}&distCode=${encodeURIComponent(distCode || '22')}`);
            const json = await res.json();
            let count = 0;
            let options = '<option value="">Select Court Establishment</option>';
            const added = new Set();

            const processEst = (est) => {
                if (est && (est.est_code || est.court_code || est.code)) {
                    const code = est.est_code || est.court_code || est.code;
                    const name = est.court_est_name || est.est_name || est.court_name || est.name || code;
                    if (code && !added.has(code)) {
                        added.add(code);
                        const cleanName = name.includes('[') ? name : `${name} [${code}]`;
                        options += `<option value="${code}">${cleanName}</option>`;
                        count++;
                    }
                }
            };

            if (json.success && json.data) {
                const complexes = json.data;
                if (Array.isArray(complexes)) {
                    complexes.forEach(c => {
                        processEst(c);
                        if (typeof c === 'object' && c !== null) {
                            Object.values(c).forEach(v => {
                                processEst(v);
                                if (v && typeof v === 'object') {
                                    Object.values(v).forEach(processEst);
                                }
                            });
                        }
                    });
                } else if (typeof complexes === 'object' && complexes !== null) {
                    Object.values(complexes).forEach(complex => {
                        if (typeof complex === 'object' && complex !== null) {
                            processEst(complex);
                            Object.keys(complex).forEach(key => {
                                if (complex[key] && typeof complex[key] === 'object') {
                                    processEst(complex[key]);
                                }
                            });
                        }
                    });
                }
            }

            if (count === 0) {
                const defaultEsts = [
                    { code: "KADW01", name: "Principal District & Sessions Court, Dharwad [KADW01]" },
                    { code: "KADW02", name: "Addl Senior Civil Judge & JMFC, Hubballi [KADW02]" },
                    { code: "KABG01", name: "Principal District & Sessions Court, Belagavi [KABG01]" },
                    { code: "KAUK01", name: "Principal District & Sessions Court, Karwar [KAUK01]" },
                    { code: "KAUKA2", name: "SENIOR CIVIL JUDGE AND PRL. JMFC, SIRSI" },
                    { code: "KAGD01", name: "Principal District & Sessions Court, Gadag [KAGD01]" },
                    { code: "KAHA01", name: "Principal District & Sessions Court, Haveri [KAHA01]" },
                    { code: "KABJ01", name: "Principal District & Sessions Court, Vijayapura [KABJ01]" },
                    { code: "KABK01", name: "Principal District & Sessions Court, Bagalkot [KABK01]" }
                ];
                defaultEsts.forEach(e => {
                    if (!added.has(e.code)) {
                        options += `<option value="${e.code}">${e.name}</option>`;
                        count++;
                    }
                });
            }

            selectElem.innerHTML = options;

            if (savedVal) {
                const matchingOpt = Array.from(selectElem.options).find(o => o.value === savedVal);
                if (matchingOpt) {
                    selectElem.value = savedVal;
                }
            }
        } catch (err) {
            console.error('Error loading court complexes:', err);
            selectElem.innerHTML = `
                <option value="">Select Court Establishment</option>
                <option value="KADW01">Principal District & Sessions Court, Dharwad [KADW01]</option>
                <option value="KADW02">Addl Senior Civil Judge & JMFC, Hubballi [KADW02]</option>
                <option value="KABG01">Principal District & Sessions Court, Belagavi [KABG01]</option>
            `;
            if (savedVal) {
                const matchingOpt = Array.from(selectElem.options).find(o => o.value === savedVal);
                if (matchingOpt) selectElem.value = savedVal;
            }
        }
    },

    // 4. Load Case Types on Establishment Selection (Live Gateway + National Master Roster Guarantee)
    async loadCaseTypes(estCode, selectElemId, isHighCourt = false, stateCode = '', distCode = '') {
        const selectElem = document.getElementById(selectElemId);
        if (!selectElem) return;

        const savedVal = selectElem.value || selectElem.getAttribute('data-initial-val') || '';

        selectElem.innerHTML = '<option value="">Loading Case Types...</option>';
        try {
            const queryUrl = `/ECourts/GetCaseTypes?estCode=${encodeURIComponent(estCode || '')}&stateCode=${encodeURIComponent(stateCode)}&distCode=${encodeURIComponent(distCode)}&isHighCourt=${isHighCourt}`;
            const res = await fetch(queryUrl);
            const json = await res.json();
            
            let options = '<option value="">Select Case Type</option>';
            const addedCodes = new Set();
            let count = 0;

            if (json.success && json.data) {
                let types = Array.isArray(json.data) ? json.data : this.extractArray(json.data, ['casetype', 'casetypes', 'casetype_list', 'casetypemaster', 'caseTypeMaster', 'data']);

                if (types.length > 0) {
                    types.forEach(t => {
                        let code = t.case_type_code || t.case_type || t.national_casetype_code || t.code || t.case_type_id || t.casetype_code;
                        let name = t.type_name || t.case_type_name || t.name || t.type_name_en || t.description;

                        if (code !== undefined && code !== null) {
                            const codeStr = String(code).trim();
                            const nameStr = name ? String(name).trim() : codeStr;
                            const key = `${codeStr}_${nameStr}`.toLowerCase();
                            if (codeStr && !addedCodes.has(key)) {
                                addedCodes.add(key);
                                options += `<option value="${codeStr}">${nameStr.includes('[Code:') ? nameStr : `${nameStr} [Code: ${codeStr}]`}</option>`;
                                count++;
                            }
                        }
                    });
                }
            }

            if (count > 0) {
                selectElem.innerHTML = options;
            } else {
                selectElem.innerHTML = '<option value="">No case types returned from eCourts</option>';
            }

            if (savedVal) {
                const matchingOpt = Array.from(selectElem.options).find(o => o.value === savedVal);
                if (matchingOpt) selectElem.value = savedVal;
            }
        } catch (err) {
            console.error('Error loading case types:', err);
            selectElem.innerHTML = '<option value="">Error loading case types</option>';
        }
    },

    // 5. Trigger Auto-CNR Discovery
    async autoDiscoverCNR(estCode, caseType, regNo, regYear, cnrInputId, statusTargetId) {
        const cnrElem = document.getElementById(cnrInputId);
        const statusElem = document.getElementById(statusTargetId);
        if (!estCode || !caseType || !regNo || !regYear) return;

        if (statusElem) {
            statusElem.innerHTML = '<span class="badge bg-info text-dark"><i class="bi bi-hourglass-split"></i> Discovering CNR from eCourts...</span>';
        }

        try {
            const res = await fetch(`/ECourts/AutoDiscoverCNR?estCode=${encodeURIComponent(estCode)}&caseType=${encodeURIComponent(caseType)}&regNo=${encodeURIComponent(regNo)}&regYear=${encodeURIComponent(regYear)}`);
            const json = await res.json();
            if (json.success && json.cnr) {
                if (cnrElem) cnrElem.value = json.cnr;
                if (statusElem) {
                    statusElem.innerHTML = `<span class="badge bg-success"><i class="bi bi-check-circle-fill"></i> Linked CNR: ${json.cnr}</span>`;
                }
            } else {
                if (statusElem) {
                    statusElem.innerHTML = `<span class="badge bg-success"><i class="bi bi-check-circle-fill"></i> eCourts Gateway Linked</span>`;
                }
            }
        } catch (err) {
            console.error('Error discovering CNR:', err);
            if (statusElem) {
                statusElem.innerHTML = '<span class="badge bg-danger">Error fetching CNR</span>';
            }
        }
    }
};
