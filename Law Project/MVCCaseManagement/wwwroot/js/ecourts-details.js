// ecourts-details.js
// Static JS for eCourts Live Sync on Case Details page.
// Extracted from @section Scripts so Razor does NOT HTML-encode => and ${...}.

/* ────────────────────────────────────────────────────
   Utilities
──────────────────────────────────────────────────── */
function setElem(id, val) {
    var el = document.getElementById(id);
    if (el && val && String(val).trim().length > 0) {
        el.innerText = String(val).trim();
        if (el.hasAttribute('title')) el.setAttribute('title', String(val).trim());
    }
}

// Deep-walk a JS object and collect all string leaf values keyed by path,
// automatically unpacking stringified JSON
function flattenObj(obj, prefix, out) {
    if (!obj) return;

    if (typeof obj === 'string') {
        var trimmed = obj.trim();
        if ((trimmed.startsWith('{') && trimmed.endsWith('}')) ||
            (trimmed.startsWith('[') && trimmed.endsWith(']'))) {
            try {
                var parsed = JSON.parse(trimmed);
                flattenObj(parsed, prefix, out);
                return;
            } catch (e) { }
        }
        if (trimmed.length > 0 && prefix) {
            var keyOnly = prefix.split('.').pop().split('[').shift().toLowerCase();
            out[keyOnly] = trimmed;
            out[prefix.toLowerCase()] = trimmed;
        }
        return;
    }

    if (Array.isArray(obj)) {
        obj.forEach(function(item, i) {
            flattenObj(item, prefix ? (prefix + '[' + i + ']') : ('[' + i + ']'), out);
        });
        return;
    }

    if (typeof obj === 'object') {
        Object.keys(obj).forEach(function(k) {
            var v = obj[k];
            var fullKey = prefix ? (prefix + '.' + k) : k;
            if (typeof v === 'string') {
                var trimmed = v.trim();
                if ((trimmed.startsWith('{') && trimmed.endsWith('}')) ||
                    (trimmed.startsWith('[') && trimmed.endsWith(']'))) {
                    try {
                        var parsed = JSON.parse(trimmed);
                        flattenObj(parsed, fullKey, out);
                        return;
                    } catch (e) { }
                }
                if (trimmed.length > 0) {
                    out[k.toLowerCase()] = trimmed;
                    out[fullKey.toLowerCase()] = trimmed;
                }
            } else if (typeof v === 'object' && v !== null) {
                flattenObj(v, fullKey, out);
            }
        });
    }
}

// Find a value in a flattened map by partial key match
function pickByKeywords(flat) {
    var keywords = Array.prototype.slice.call(arguments, 1);
    var keys = Object.keys(flat);
    for (var i = 0; i < keys.length; i++) {
        var key = keys[i];
        var allMatch = keywords.every(function(kw) { return key.includes(kw); });
        if (allMatch) return flat[key];
    }
    return null;
}

// Parse a date string in any common format and return a Date object (or null)
function parseAnyDate(s) {
    if (!s) return null;
    var d = new Date(s);
    if (!isNaN(d)) return d;
    // dd-MM-yyyy or dd/MM/yyyy
    var m = s.match(/^(\d{1,2})[-\/](\d{1,2})[-\/](\d{4})$/);
    if (m) return new Date(+m[3], +m[2]-1, +m[1]);
    return null;
}

// Extract valid date string (YYYY-MM-DD or DD-MM-YYYY) ignoring textual placeholders like "Next Date Not Given"
function extractValidDate(str) {
    if (!str || typeof str !== 'string') return null;
    var s = str.trim();
    if (s.toLowerCase().includes('not given') || s === '—' || s === '-' || s.length < 8) return null;
    var m = s.match(/\b\d{4}-\d{2}-\d{2}\b/) || s.match(/\b\d{1,2}[-\/]\d{1,2}[-\/]\d{4}\b/);
    return m ? m[0] : null;
}

// Decode HTML entities (including doubly-encoded entities like &amp;ldquo; -> ")
function decodeHtmlEntities(str) {
    if (!str || typeof str !== 'string') return '';
    var txt = document.createElement('textarea');
    txt.innerHTML = str;
    var val = txt.value;
    if (val.indexOf('&') !== -1) {
        txt.innerHTML = val;
        val = txt.value;
    }
    return val;
}

// Universal array/object scanner: extracts all history and order objects,
// automatically unpacking stringified JSON strings and object-keyed dictionaries (like historyofcasehearing, interimorder)
function collectAllArrays(obj) {
    var historyList = [];
    var ordersList = [];
    var visited = [];

    function processItem(item, keyName) {
        if (!item || typeof item !== 'object' || visited.indexOf(item) !== -1) return;
        visited.push(item);
        var keys = Object.keys(item).map(function(k) { return k.toLowerCase(); });
        var lk = (keyName || '').toLowerCase();

        var histKeyMatches = ['historyofcasehearing', 'history', 'history_details', 'case_history', 'hearing_history', 'hist_details', 'causelist_history', 'hearings', 'businessdetails', 'case_hearings', 'casehistory', 'business_details', 'hist', 'case_business'].some(function(hk) { return lk.indexOf(hk) !== -1; });
        var hasHistFields = keys.some(function(k) {
            return k === 'business_date' || k === 'next_date' || k === 'next_purpose' || k === 'srno' || k === 'sr_no' || k === 'cause_date' || k === 'purpose_name' || k === 'hearing_date';
        });

        var orderKeyMatches = ['interimorder', 'finalorder', 'orders', 'order_details', 'orders_details', 'case_orders', 'judgements', 'judgments', 'judgment_details', 'order_list', 'final_order', 'orderdetails', 'judgment_copy', 'order_copy', 'order_pdf', 'judgment', 'interim_order', 'interim_orders'].some(function(ok) { return lk.indexOf(ok) !== -1; });
        var hasOrderFields = keys.some(function(k) {
            return k === 'order_no' || k === 'order_date' || k === 'pdf_path' || k === 'pdf_url' || k === 'order_pdf' || k === 'order_details' || k === 'order_type' || k === 'pdf_name' || k === 'filename';
        });

        if (orderKeyMatches || hasOrderFields) ordersList.push(item);
        if (histKeyMatches || hasHistFields) historyList.push(item);
    }

    function scan(val, keyName) {
        if (!val) return;
        if (keyName === undefined) keyName = '';

        if (typeof val === 'string') {
            var trimmed = val.trim();
            if ((trimmed.startsWith('[') && trimmed.endsWith(']')) ||
                (trimmed.startsWith('{') && trimmed.endsWith('}'))) {
                try {
                    var parsed = JSON.parse(trimmed);
                    scan(parsed, keyName);
                } catch (e) { }
            }
            return;
        }

        if (Array.isArray(val)) {
            val.forEach(function(item) { processItem(item, keyName); });
            val.forEach(function(item) { scan(item, keyName); });
            return;
        }

        if (typeof val === 'object') {
            var keys = Object.keys(val);
            var isDictObject = keys.length > 0 && keys.every(function(k) { return /^sr_no\d+$/i.test(k) || /^sr\d+$/i.test(k) || /^order\d+$/i.test(k) || /^\d+$/.test(k); });
            var lk = keyName.toLowerCase();
            var isTargetKey = lk.includes('hist') || lk.includes('order') || lk.includes('hear') || lk.includes('judg');

            if (isTargetKey) {
                processItem(val, keyName);
            }

            if (isDictObject || isTargetKey) {
                keys.forEach(function(k) {
                    if (val[k] && typeof val[k] === 'object') {
                        processItem(val[k], keyName);
                    }
                });
            }

            keys.forEach(function(k) { scan(val[k], k); });
        }
    }

    scan(obj, '');
    return { historyList: historyList, ordersList: ordersList };
}

/* ────────────────────────────────────────────────────
   getObjProp: look up a property by fuzzy keyword list
──────────────────────────────────────────────────── */
function getObjProp(item) {
    if (!item || typeof item !== 'object') return null;
    var keywords = Array.prototype.slice.call(arguments, 1);
    var keys = Object.keys(item);
    for (var ki = 0; ki < keywords.length; ki++) {
        var kw = keywords[ki];
        var foundKey = null;
        for (var j = 0; j < keys.length; j++) {
            if (keys[j].toLowerCase() === kw.toLowerCase()) { foundKey = keys[j]; break; }
        }
        if (!foundKey && kw.length > 2) {
            for (var j = 0; j < keys.length; j++) {
                if (keys[j].toLowerCase().includes(kw.toLowerCase())) { foundKey = keys[j]; break; }
            }
        }
        if (foundKey && item[foundKey]) {
            var val = String(item[foundKey]).trim();
            if (val.length > 0) return val;
        }
    }
    return null;
}

/* ────────────────────────────────────────────────────
   Render: Case Hearing History table
──────────────────────────────────────────────────── */
function renderHearingHistory(items) {
    window.lastHistoryItems = items || [];
    var container = document.getElementById('live-history-container');
    var badge = document.getElementById('history-count-badge');
    if (!container) return;

    if (!items || items.length === 0) {
        if (badge) badge.innerText = '0 Hearings';
        container.innerHTML =
            '<div class="alert alert-light text-muted border text-center py-3 mb-0 small">' +
            '<i class="bi bi-calendar-x me-1"></i> No hearing history records returned by eCourts Gateway.' +
            '</div>';
        return;
    }

    if (badge) badge.innerText = items.length + ' Hearing' + (items.length > 1 ? 's' : '');

    // Get fallback judge and top-level next date from savedCNR data
    var defaultJudge = (window.lastLiveCnrData && window.lastLiveCnrData.court_no_judge) ||
                       (window.lastLiveCnrData && window.lastLiveCnrData.judge) || '\u2014';
    var topNextDate = (window.lastLiveCnrData && window.lastLiveCnrData.next_hearing_date) || '';

    var rows = '';
    items.forEach(function(h, i) {
        var sr = getObjProp(h, 'srno', 'sr_no', 'sno', 'sr') || (i + 1);

        var judge = getObjProp(h, 'judge', 'court_no_judge', 'judge_name', 'presiding_officer', 'court_judge', 'desgname');
        if (!judge || judge === '\u2014') judge = defaultJudge;

        var businessDate = getObjProp(h, 'business_date', 'cause_date', 'dt_business', 'hist_date', 'srno_date') || '—';
        var hearingDate = getObjProp(h, 'hearing_date', 'next_date', 'next_hearing_date', 'nxt_date', 'next_date_of_hearing', 'date_next_list');
        if ((!businessDate || businessDate === '—') && h.hearing_date && !h.next_date) {
            businessDate = '—';
            hearingDate = h.hearing_date;
        }

        if (!hearingDate || hearingDate === '—') {
            // Intelligent resolution: use next row's business date, or top-level next date for the last entry
            if (i < items.length - 1) {
                var nextRowBusDate = getObjProp(items[i + 1], 'business_date', 'cause_date', 'dt_business', 'hist_date', 'srno_date');
                if (nextRowBusDate && nextRowBusDate !== '—') hearingDate = nextRowBusDate;
            } else if (topNextDate) {
                hearingDate = topNextDate;
            }
        }
        if (!hearingDate) hearingDate = '—';

        var purpose = getObjProp(h, 'purpose_of_listing', 'next_purpose', 'purpose_name', 'purpose', 'court_stage', 'stage', 'purpose_of_hearing') || '—';
        // Use exact key matching for 'business' to avoid fuzzy match returning business_date
        var businessKeys = ['business', 'roznama', 'proceedings', 'court_business', 'business_details', 'order_business', 'short_order'];
        var businessRaw = null;
        var hLower = Object.keys(h).reduce(function(m, k) { m[k.toLowerCase()] = h[k]; return m; }, {});
        for (var bki = 0; bki < businessKeys.length; bki++) {
            var bv = hLower[businessKeys[bki]];
            if (bv && typeof bv === 'string' && bv.trim().length > 5 && !/^(null|undefined|n\/a|na|none|—|--)$/i.test(bv.trim()) && !/^\d{2,4}[\-\/]/.test(bv.trim())) {
                businessRaw = bv.trim(); break;
            }
        }
        var business = businessRaw || '';

        var businessHtml = (business && business !== '—' && business.toLowerCase() !== purpose.toLowerCase())
            ? '<div class="small text-dark text-break" style="max-width:320px;"><i class="bi bi-journal-text text-info me-1"></i>' + business + '</div>'
            : '<span class="text-muted small">—</span>';

        var hearingLink = (hearingDate && hearingDate !== '—')
            ? '<a href="javascript:void(0)" onclick="openHearingDetailsModal(\'DC\', ' + i + ')" class="text-primary text-decoration-none fw-bold" title="Click to view eCourts Daily Status"><i class="bi bi-box-arrow-up-right me-1 extra-small"></i>' + hearingDate + '</a>'
            : '<span class="text-muted">—</span>';

        var busLink = (businessDate && businessDate !== '—')
            ? '<a href="javascript:void(0)" onclick="openHearingDetailsModal(\'DC\', ' + i + ')" class="text-secondary text-decoration-none fw-semibold" title="Click to view eCourts Daily Status on this date">' + businessDate + '</a>'
            : '<span class="text-muted">—</span>';

        var liveDailyBtn = '<button type="button" onclick="openHearingDetailsModal(\'DC\', ' + i + ')" class="btn btn-sm btn-primary py-0 px-2 rounded-pill extra-small shadow-sm mt-1" title="View Official eCourts Daily Status">' +
            '<i class="bi bi-journal-text me-1"></i>Daily Status</button>';

        rows +=
            '<tr>' +
            '<td class="fw-bold text-muted small text-center">' + sr + '</td>' +
            '<td class="small text-truncate" style="max-width:200px;" title="' + judge + '">' + judge + '</td>' +
            '<td class="fw-semibold text-secondary small">' + busLink + '</td>' +
            '<td class="fw-semibold text-primary small">' + hearingLink + '</td>' +
            '<td><span class="badge bg-secondary">' + purpose + '</span></td>' +
            '<td>' + businessHtml + '<div class="mt-1">' + liveDailyBtn + '</div></td>' +
            '</tr>';
    });

    container.innerHTML =
        '<div class="table-responsive" style="max-height: 420px; overflow-y: auto;">' +
        '<table class="table table-sm table-hover align-middle mb-0">' +
        '<thead class="table-light sticky-top">' +
        '<tr>' +
        '<th style="width:36px;" class="text-center">#</th>' +
        '<th>Judge / Court</th>' +
        '<th>Business on Date</th>' +
        '<th>Hearing Date</th>' +
        '<th>Purpose of Hearing</th>' +
        '<th>Business Transacted / Roznama</th>' +
        '</tr>' +
        '</thead>' +
        '<tbody>' + rows + '</tbody>' +
        '</table>' +
        '</div>';
}

// In-app Order Viewer Modal (Opens documents right inside the page without redirecting)
function openOrderInModal(targetUrl, title, cnr, date, caseId, orderNo) {
    var modalElem = document.getElementById('ecourtsOrderModal');
    if (!modalElem) {
        var modalHtml =
            '<div class="modal fade" id="ecourtsOrderModal" tabindex="-1" aria-hidden="true">' +
            '<div class="modal-dialog modal-xl modal-dialog-centered" style="height: 85vh;">' +
            '<div class="modal-content h-100 shadow-lg border-0">' +
            '<div class="modal-header bg-dark text-white py-2 px-3">' +
            '<h6 class="modal-title fw-bold" id="ecourtsOrderModalTitle"><i class="bi bi-file-earmark-pdf text-danger me-2"></i>Court Order Document</h6>' +
            '<div class="d-flex align-items-center gap-2">' +
            '<a href="#" id="ecourtsOrderOpenNewTab" target="_blank" class="btn btn-sm btn-outline-light py-0"><i class="bi bi-box-arrow-up-right me-1"></i>New Tab</a>' +
            '<button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>' +
            '</div>' +
            '</div>' +
            '<div class="modal-body p-0 bg-dark" style="height: calc(100% - 48px);">' +
            '<iframe id="ecourtsOrderIframe" style="width:100%; height:100%; border:none;"></iframe>' +
            '</div>' +
            '</div>' +
            '</div>' +
            '</div>';
        document.body.insertAdjacentHTML('beforeend', modalHtml);
        modalElem = document.getElementById('ecourtsOrderModal');
    }

    var titleElem = document.getElementById('ecourtsOrderModalTitle');
    var iframeElem = document.getElementById('ecourtsOrderIframe');
    var newTabElem = document.getElementById('ecourtsOrderOpenNewTab');

    if (titleElem) titleElem.innerHTML = '<i class="bi bi-file-earmark-pdf text-danger me-2"></i>' + (title || 'Court Order Document');

    function setOrReplaceParam(urlStr, paramName, paramVal) {
        if (!paramVal) return urlStr;
        var re = new RegExp('([?&]' + paramName + '=)([^&]*)', 'i');
        if (re.test(urlStr)) {
            return urlStr.replace(re, function(match, p1, p2) {
                var p2Dec = decodeURIComponent(p2 || '').trim().toLowerCase();
                if (!p2Dec || p2Dec.includes('pending') || p2Dec === 'undefined' || p2Dec === 'null' || p2Dec === 'n/a') {
                    return p1 + encodeURIComponent(paramVal);
                }
                return match;
            });
        } else {
            return urlStr + (urlStr.includes('?') ? '&' : '?') + paramName + '=' + encodeURIComponent(paramVal);
        }
    }

    var currentCnr = cnr || window.lastCnr || '';
    var cid = caseId || window.lastCaseId || '';
    var proxyUrl;
    if (targetUrl && (targetUrl.startsWith('/ECourts/ViewOrderPdf') || targetUrl.startsWith('data:application/pdf'))) {
        proxyUrl = targetUrl;
        proxyUrl = setOrReplaceParam(proxyUrl, 'cnr', currentCnr);
        proxyUrl = setOrReplaceParam(proxyUrl, 'caseId', cid);
        if (orderNo) proxyUrl = setOrReplaceParam(proxyUrl, 'orderNo', orderNo);
        if (date) proxyUrl = setOrReplaceParam(proxyUrl, 'date', date);
        if (title) proxyUrl = setOrReplaceParam(proxyUrl, 'title', title);
    } else {
        proxyUrl = '/ECourts/ViewOrderPdf?url=' + encodeURIComponent(targetUrl || '') +
                   '&cnr=' + encodeURIComponent(currentCnr) +
                   '&title=' + encodeURIComponent(title || '') +
                   '&date=' + encodeURIComponent(date || '') +
                   (orderNo ? ('&orderNo=' + encodeURIComponent(orderNo)) : '') +
                   (cid ? ('&caseId=' + cid) : '');
    }

    if (iframeElem) iframeElem.src = proxyUrl;
    if (newTabElem) newTabElem.href = proxyUrl;

    if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
        var bsModal = bootstrap.Modal.getInstance(modalElem) || new bootstrap.Modal(modalElem);
        bsModal.show();
    } else {
        $(modalElem).modal('show');
    }
}

/* ────────────────────────────────────────────────────
   Render: Court Orders & Judgments cards
──────────────────────────────────────────────────── */
function renderOrdersAndJudgments(items) {
    var container = document.getElementById('live-orders-container');
    var badge = document.getElementById('orders-count-badge');
    if (!container) return;

    var cnrNumber = window.lastCnr ||
                    (window.lastLiveCnrData && window.lastLiveCnrData.cino) ||
                    (window.lastLiveCnrData && window.lastLiveCnrData.cnr) || '';

    var defaultJudge = (window.lastLiveCnrData && window.lastLiveCnrData.court_no_judge) ||
                       (window.lastLiveCnrData && window.lastLiveCnrData.judge) || '—';

    // Deduplicate by date and title
    var seen = {};
    var uniqueItems = [];
    (items || []).forEach(function(ord) {
        var titleVal = (getObjProp(ord, 'order_type', 'purpose_name', 'process_name', 'order_details') || '').toLowerCase().trim();
        var dateVal = (getObjProp(ord, 'order_date', 'date', 'order_dt', 'business_date', 'dt_regis') || '').toLowerCase().trim();
        var key = dateVal + '_' + titleVal;
        if (!seen[key]) {
            seen[key] = true;
            uniqueItems.push(ord);
        }
    });

    if (uniqueItems.length === 0) {
        if (badge) badge.innerText = '0 Files Available';
        container.innerHTML =
            '<div class="alert alert-light text-muted border text-center py-3 mb-0 small">' +
            '<i class="bi bi-info-circle me-1"></i> No court orders or judgments found in live e-Courts.</div>';
        return;
    }

    if (badge) badge.innerText = uniqueItems.length + ' File' + (uniqueItems.length > 1 ? 's' : '') + ' Available';

    var rows = '';
    var currentCaseId = window.lastCaseId || '';
    uniqueItems.forEach(function(ord, i) {
        var orderNo = getObjProp(ord, 'order_number', 'order_no', 'sr_no', 'sno');
        if (!orderNo || orderNo.length > 8 || orderNo === cnrNumber || !/^\d+$/.test(orderNo)) {
            orderNo = (i + 1).toString();
        }
        var dt = getObjProp(ord, 'order_date', 'date', 'order_dt', 'business_date', 'dt_regis', 'filing_date', 'hearing_date') || '—';
        var details = getObjProp(ord, 'order_details', 'details', 'order_type', 'title', 'type', 'purpose_name', 'process_name', 'description', 'remarks', 'nature_of_disposal') || 'Court Order / Judgment';
        var judge = getObjProp(ord, 'judge_name', 'judge', 'court_name', 'court_no_judge', 'desgname');
        if (!judge || judge === '—') judge = defaultJudge;

        var pdfUrl = getObjProp(ord, 'order_pdf_url', 'pdf_url', 'pdf_path', 'url', 'download_url', 'file_path', 'pdf_name', 'order_pdf', 'pdf', 'file_name', 'filename', 'filepath', 'order_copy', 'judgment_copy', 'doc_path', 'display_pdf', 'order_file', 'link', 'path') || '';

        var safeTitle = details.replace(/'/g, "\\'");
        var safePdfUrl = pdfUrl.replace(/'/g, "\\'");
        var safeDt = dt.replace(/'/g, "\\'");

        var pdfBtn = '<button type="button" onclick="openOrderInModal(\'' + safePdfUrl + '\', \'' + safeTitle + '\', \'' + cnrNumber + '\', \'' + safeDt + '\', \'' + currentCaseId + '\', \'' + orderNo + '\')" class="btn btn-sm btn-danger rounded-pill px-3 shadow-sm">' +
                     '<i class="bi bi-file-earmark-pdf me-1"></i> View Court Order</button>' +
                     '<a href="/ECourts/DownloadOrderPdf?pdfPath=' + encodeURIComponent(safePdfUrl) + '&cnr=' + encodeURIComponent(cnrNumber) + '&orderNo=' + encodeURIComponent(orderNo) + '&date=' + encodeURIComponent(safeDt) + '" class="btn btn-sm btn-outline-secondary rounded-pill px-2 shadow-sm ms-1" title="Download Order PDF" target="_blank">' +
                     '<i class="bi bi-download"></i></a>';

        rows +=
            '<tr class="align-middle">' +
            '<td class="fw-bold text-center py-2 px-3 text-secondary" style="width:12%;">' + orderNo + '</td>' +
            '<td class="fw-bold text-dark py-2 px-3" style="width:23%;"><i class="bi bi-calendar-check text-danger me-1"></i>' + dt + '</td>' +
            '<td class="py-2 px-3" style="width:40%;"><div class="fw-bold text-dark"><i class="bi bi-file-earmark-text text-primary me-1"></i>' + details + '</div>' +
            (judge && judge !== '—' ? '<div class="extra-small text-muted"><i class="bi bi-bank me-1"></i>' + judge + '</div>' : '') +
            '</td>' +
            '<td class="py-2 px-3 text-end" style="width:25%;">' + pdfBtn + '</td>' +
            '</tr>';
    });

    var tableHtml =
        '<div class="table-responsive rounded border bg-white shadow-sm">' +
        '<table class="table table-hover table-striped mb-0 align-middle small">' +
        '<thead class="table-dark small text-uppercase">' +
        '<tr>' +
        '<th class="text-center py-2 px-3" style="width:12%;">Order Number</th>' +
        '<th class="py-2 px-3" style="width:23%;">Order Date</th>' +
        '<th class="py-2 px-3" style="width:40%;">Order Details</th>' +
        '<th class="text-end py-2 px-3" style="width:25%;">Action</th>' +
        '</tr>' +
        '</thead>' +
        '<tbody>' + rows + '</tbody>' +
        '</table>' +
        '</div>';

    container.innerHTML = tableHtml;
}

/* ────────────────────────────────────────────────────
   Render: Interim Applications (IA Filings)
──────────────────────────────────────────────────── */
function renderIaFilings(items) {
    var container = document.getElementById('live-ia-container');
    var badge = document.getElementById('ia-count-badge');
    if (!container) return;

    if (!items || items.length === 0) {
        if (badge) badge.innerText = '0 IAs';
        container.innerHTML =
            '<div class="alert alert-light text-muted border text-center py-3 mb-0 small">' +
            '<i class="bi bi-info-circle me-1"></i> No Interim Applications (IA) recorded in live eCourts.</div>';
        return;
    }

    if (badge) badge.innerText = items.length + ' IA' + (items.length > 1 ? 's' : '');

    var rows = '';
    items.forEach(function(ia, i) {
        var iaNo = getObjProp(ia, 'ia_number', 'ia_no', 'no') || (i + 1);
        var party = getObjProp(ia, 'ia_pet_name', 'party_name', 'pet_name', 'applicant') || '—';
        var dt = getObjProp(ia, 'date_of_filing', 'filing_date', 'date') || '—';
        var prayer = getObjProp(ia, 'ia_prayer', 'prayer', 'purpose', 'relief_sought') || '—';
        var dispDt = getObjProp(ia, 'disp_date', 'disposal_date', 'decision_date') || '';
        var status = getObjProp(ia, 'ia_pend_disp', 'status', 'pend_disp') || '—';
        if (status === 'P' || status.toLowerCase().includes('pend')) status = '<span class="badge bg-warning text-dark">Pending</span>';
        else if (status === 'D' || status.toLowerCase().includes('disp')) status = '<span class="badge bg-success">Disposed' + (dispDt ? ' (' + dispDt + ')' : '') + '</span>';
        else status = '<span class="badge bg-secondary">' + status + '</span>';

        rows +=
            '<tr>' +
            '<td class="fw-bold text-center text-muted small">' + (i + 1) + '</td>' +
            '<td class="fw-bold text-dark small">' + iaNo + '</td>' +
            '<td class="small text-truncate" style="max-width:220px;" title="' + party + '">' + party + '</td>' +
            '<td class="small text-truncate" style="max-width:280px;" title="' + prayer + '"><i class="bi bi-card-text text-primary me-1"></i>' + prayer + '</td>' +
            '<td class="fw-semibold text-secondary small">' + dt + '</td>' +
            '<td>' + status + '</td>' +
            '</tr>';
    });

    container.innerHTML =
        '<div class="table-responsive rounded border bg-white shadow-sm">' +
        '<table class="table table-sm table-hover align-middle mb-0">' +
        '<thead class="table-light small text-uppercase">' +
        '<tr><th class="text-center" style="width:36px;">#</th><th>IA Number</th><th>Applicant / Party</th><th>Prayer / Relief</th><th>Date of Filing</th><th>Status</th></tr>' +
        '</thead>' +
        '<tbody>' + rows + '</tbody>' +
        '</table>' +
        '</div>';
}

/* ────────────────────────────────────────────────────
   Render: Process / Notices Service
──────────────────────────────────────────────────── */
function renderProcesses(items) {
    var container = document.getElementById('live-process-container');
    var badge = document.getElementById('process-count-badge');
    if (!container) return;

    if (!items || items.length === 0) {
        if (badge) badge.innerText = '0 Notices';
        container.innerHTML =
            '<div class="alert alert-light text-muted border text-center py-3 mb-0 small">' +
            '<i class="bi bi-info-circle me-1"></i> No process or notice records found in live eCourts.</div>';
        return;
    }

    if (badge) badge.innerText = items.length + ' Notice' + (items.length > 1 ? 's' : '');

    var rows = '';
    items.forEach(function(p, i) {
        var pId = getObjProp(p, 'process_id', 'id', 'process_no') || ('P-' + (i + 1));
        var pDt = getObjProp(p, 'process_date', 'date', 'dt', 'issue_date') || '—';
        var pTitle = getObjProp(p, 'process_title', 'title', 'process_name', 'details', 'notice_type') || 'Notice / Summons';
        var party = getObjProp(p, 'party_name', 'served_to', 'recipient', 'party') || '—';
        var returnDt = getObjProp(p, 'return_date', 'next_date', 'compliance_date', 'status') || '—';

        var pIdLink = '<a href="javascript:void(0)" onclick="openProcessDetailsModal(\'DC\', ' + i + ')" class="text-primary text-decoration-none fw-bold" title="Click to view notice & process details"><i class="bi bi-box-arrow-up-right me-1 extra-small"></i>' + pId + '</a>';
        var retLink = (returnDt && returnDt !== '—')
            ? '<a href="javascript:void(0)" onclick="openProcessDetailsModal(\'DC\', ' + i + ')" class="text-primary text-decoration-none fw-semibold">' + returnDt + '</a>'
            : '<span class="text-muted">—</span>';

        rows +=
            '<tr>' +
            '<td class="fw-bold text-center text-muted small">' + (i + 1) + '</td>' +
            '<td class="font-monospace small fw-bold text-primary">' + pIdLink + '</td>' +
            '<td class="fw-semibold text-secondary small">' + pDt + '</td>' +
            '<td class="small text-dark fw-semibold">' + pTitle + '</td>' +
            '<td class="small text-secondary text-truncate" style="max-width:200px;" title="' + party + '">' + party + '</td>' +
            '<td class="small text-primary fw-semibold">' + retLink + '</td>' +
            '</tr>';
    });

    container.innerHTML =
        '<div class="table-responsive rounded border bg-white shadow-sm">' +
        '<table class="table table-sm table-hover align-middle mb-0">' +
        '<thead class="table-light small text-uppercase">' +
        '<tr><th class="text-center" style="width:36px;">#</th><th>Process ID</th><th>Issue Date</th><th>Process / Notice Title</th><th>Served To / Party</th><th>Return / Status</th></tr>' +
        '</thead>' +
        '<tbody>' + rows + '</tbody>' +
        '</table>' +
        '</div>';
}

/* ────────────────────────────────────────────────────
   Main: fetchLiveCnrDetails
──────────────────────────────────────────────────── */
function fetchLiveCnrDetails(cnr, caseId) {
    if (caseId) window.lastCaseId = caseId;
    if (cnr) window.lastCnr = cnr;
    var msgElem = document.getElementById('live-sync-msg');

    if (!cnr || String(cnr).trim().length < 5) {
        if (msgElem) {
            msgElem.innerHTML = '<span class="text-secondary"><i class="bi bi-info-circle me-1"></i> CNR Number is not linked. Auto-discover or link CNR to fetch live status.</span>';
        }
        renderHearingHistory([]);
        renderOrdersAndJudgments([]);
        return;
    }

    if (msgElem) {
        msgElem.innerHTML = '<span class="text-info"><i class="bi bi-hourglass-split me-1"></i> Querying live eCourts Gateway...</span>';
    }

    var queryUrl = '/ECourts/GetCnrDetails?cnrNumber=' + encodeURIComponent(cnr || '') + (caseId ? '&caseId=' + caseId : '');

    fetch(queryUrl)
        .then(function(r) { return r.json(); })
        .then(function(json) {
            if (!json.success) {
                if (msgElem) msgElem.innerHTML = '<span class="text-warning"><i class="bi bi-exclamation-circle me-1"></i> ' + (json.message || 'No live records returned by eCourts Gateway.') + '</span>';
                renderHearingHistory([]);
                renderOrdersAndJudgments([]);
                renderIaFilings([]);
                renderProcesses([]);
                ['live-first-date', 'live-next-date', 'live-stage', 'live-case-status', 'live-filing-date', 'live-decision-date', 'live-court-judge', 'live-pet-adv', 'live-petitioner', 'live-respondent', 'live-extra-party'].forEach(function(id) {
                    var el = document.getElementById(id);
                    if (el) el.innerText = '—';
                });
                return;
            }

            if (msgElem) msgElem.innerHTML = '<span class="text-success"><i class="bi bi-check-circle-fill me-1"></i> Live eCourts Gateway \u2013 Synced</span>';

            console.group('eCourts CNR Live Response');
            console.log('Parsed data:', json.data);
            console.log('Raw API response:', json.raw);
            console.groupEnd();

            // Build a flat map of all string values from the raw response
            var flat = {};
            if (json.raw) flattenObj(json.raw, '', flat);

            // Helper: get from parsed data first, then fall back to raw flat scan
            function get(parsedVal) {
                var rawKeywords = Array.prototype.slice.call(arguments, 1);
                if (parsedVal && String(parsedVal).trim().length > 0) return String(parsedVal).trim();
                return pickByKeywords.apply(null, [flat].concat(rawKeywords));
            }

            var d = json.data || {};
            window.lastLiveCnrData = d;

            // Dynamically update top CNR badge if CNR was discovered/returned by server
            var activeCnr = d.cnr_number || (json.summary ? json.summary.cnr_number : '') || cnr;
            if (activeCnr && activeCnr.length >= 10 && !activeCnr.includes('PENDING')) {
                var badgeEl = document.getElementById('details-cnr-badge');
                if (badgeEl) {
                    badgeEl.innerHTML = '<span class="badge bg-success font-monospace fs-6 px-3 py-2 shadow-sm"><i class="bi bi-check-circle-fill me-1"></i> CNR: ' + activeCnr + '</span>';
                }
            }

            // ── Next Hearing Date ──────────────────────────────────────
            var nextDateCandidates = [];
            Object.keys(flat).forEach(function(k) {
                if ((k.includes('next') || k.includes('nxt')) && !k.includes('first')) {
                    var dt = parseAnyDate(flat[k]);
                    if (dt) nextDateCandidates.push({ raw: flat[k], dt: dt });
                }
            });

            // Scan history arrays for dates
            var allHistoryDates = [];
            function scanHistoryDates(obj) {
                if (!obj || typeof obj !== 'object') return;
                if (Array.isArray(obj)) { obj.forEach(scanHistoryDates); return; }
                Object.keys(obj).forEach(function(k) {
                    var v = obj[k];
                    if ((k.toLowerCase().includes('date') || k.toLowerCase().includes('dt')) && typeof v === 'string') {
                        var dt = parseAnyDate(v);
                        if (dt && !k.toLowerCase().includes('filing') &&
                            !k.toLowerCase().includes('decision') &&
                            !k.toLowerCase().includes('disposal')) {
                            allHistoryDates.push({ raw: v, dt: dt, key: k });
                        }
                    }
                    if (typeof v === 'object') scanHistoryDates(v);
                });
            }
            if (json.raw) {
                Object.keys(json.raw || {}).forEach(function(k) {
                    if (Array.isArray(json.raw[k])) json.raw[k].forEach(scanHistoryDates);
                });
            }

            // Check if case is disposed
            var stageStr = (d.stage || '').toLowerCase();
            var statusStr = (d.case_status || '').toLowerCase();
            var isCaseDisposed = stageStr.includes('disposed') || statusStr.includes('disposed') || (d.next_hearing_date && d.next_hearing_date.toLowerCase().includes('disposed'));

            // Pick latest date
            var latestDate = d.next_hearing_date ? { raw: d.next_hearing_date, dt: parseAnyDate(d.next_hearing_date) } : null;
            nextDateCandidates.forEach(function(c) {
                if (!latestDate || c.dt > latestDate.dt) latestDate = c;
            });
            if (!latestDate && allHistoryDates.length > 0) {
                latestDate = allHistoryDates.reduce(function(a, b) { return a.dt > b.dt ? a : b; });
            }

            var nextDateElem = document.getElementById('live-next-date');
            if (nextDateElem) {
                if (isCaseDisposed) {
                    nextDateElem.innerText = 'Disposed';
                    nextDateElem.className = 'fw-bold text-danger fs-6';
                } else if (latestDate) {
                    nextDateElem.innerText = latestDate.raw;
                    nextDateElem.className = 'fw-bold text-success fs-6';
                }
            }

            // ── First Hearing Date ────────────────────────────────────
            setElem('live-first-date', d.first_hearing_date || get(d.first_hearing_date, 'first'));

            // ── Stage ─────────────────────────────────────────────────
            setElem('live-stage', d.stage || get(d.stage, 'stage'));

            // ── Court Number & Judge ──────────────────────────────────
            var courtJudge = d.court_no_judge || get(d.court_no_judge, 'court', 'judge') || get(d.court_no_judge, 'court', 'no');
            if (!courtJudge) {
                var cn = pickByKeywords(flat, 'court', 'no') || pickByKeywords(flat, 'courtno');
                var jn = pickByKeywords(flat, 'judge', 'name') || pickByKeywords(flat, 'judge');
                courtJudge = (cn && jn) ? (cn + ' \u2014 ' + jn) : (cn || jn);
            }
            setElem('live-court-judge', courtJudge);

            // ── Petitioner ────────────────────────────────────────────
            setElem('live-petitioner',
                d.petitioner ||
                get(d.petitioner, 'petitioner') ||
                pickByKeywords(flat, 'pet', 'name') ||
                pickByKeywords(flat, 'plaintiff') ||
                pickByKeywords(flat, 'appellant')
            );

            // ── Petitioner Advocate ───────────────────────────────────
            setElem('live-pet-adv',
                d.petitioner_advocate ||
                get(d.petitioner_advocate, 'pet_adv') ||
                pickByKeywords(flat, 'pet_adv') ||
                pickByKeywords(flat, 'pet', 'adv') ||
                pickByKeywords(flat, 'petitioner', 'advocate')
            );

            // ── Respondent ────────────────────────────────────────────
            setElem('live-respondent',
                d.respondent ||
                get(d.respondent, 'res_name') ||
                pickByKeywords(flat, 'res_name') ||
                pickByKeywords(flat, 'respondent')
            );

            // ── Extra Parties / Insurance Company ─────────────────────
            var extraPartyVal = d.extra_party;
            if (!extraPartyVal && json.raw && json.raw.res_extra_party) {
                if (typeof json.raw.res_extra_party === 'object') {
                    extraPartyVal = Object.values(json.raw.res_extra_party).join(', ');
                } else if (typeof json.raw.res_extra_party === 'string') {
                    extraPartyVal = json.raw.res_extra_party;
                }
            }
            if (!extraPartyVal) {
                extraPartyVal = pickByKeywords(flat, 'extra_party') || pickByKeywords(flat, 'party_no1');
            }
            setElem('live-extra-party', extraPartyVal);

            // ── Filing Date ───────────────────────────────────────────
            setElem('live-filing-date',
                d.filing_date ||
                get(d.filing_date, 'filing') ||
                pickByKeywords(flat, 'reg', 'date') ||
                pickByKeywords(flat, 'registration', 'date')
            );

            // ── Decision Date ─────────────────────────────────────────
            var decisionDateVal =
                d.decision_date ||
                get(d.decision_date, 'decision') ||
                pickByKeywords(flat, 'disposal', 'date') ||
                pickByKeywords(flat, 'decision', 'date') ||
                pickByKeywords(flat, 'date', 'decision') ||
                pickByKeywords(flat, 'date_of_decision') ||
                pickByKeywords(flat, 'disposal_date');

            var currentStageText = (d.stage || '').toLowerCase();
            var currentStatusText = (d.case_status || '').toLowerCase();

            if (!decisionDateVal && (currentStageText.includes('disposed') || currentStageText.includes('decided') || currentStatusText.includes('disposed') || currentStatusText.includes('decided'))) {
                if (latestDate && latestDate.raw) {
                    decisionDateVal = latestDate.raw;
                } else if (allHistoryDates.length > 0) {
                    decisionDateVal = allHistoryDates[allHistoryDates.length - 1].raw;
                }
            }

            var decisionElem = document.getElementById('live-decision-date');
            if (decisionElem) {
                if (decisionDateVal) {
                    decisionElem.innerText = decisionDateVal;
                    decisionElem.className = 'fw-semibold text-danger';
                } else if (currentStageText.includes('disposed') || currentStatusText.includes('disposed')) {
                    decisionElem.innerText = 'Disposed';
                    decisionElem.className = 'fw-semibold text-success';
                } else {
                    decisionElem.innerText = 'Not Decided (Pending)';
                    decisionElem.className = 'fw-semibold text-muted fst-italic small';
                }
            }

            // ── Case Status ───────────────────────────────────────────
            function isValidStatusText(str) {
                if (!str || typeof str !== 'string') return false;
                var t = str.trim();
                if (t.length === 0) return false;
                if (t.toLowerCase().includes('invalid') ||
                    t.toLowerCase().includes('token') ||
                    t.toLowerCase().includes('error') ||
                    t.toLowerCase().includes('fail') ||
                    t.startsWith('wh+')) return false;
                if (t.length > 20 && (t.includes('+') || t.includes('/') || t.includes('=') || !t.includes(' '))) return false;
                return true;
            }

            var rawStatus = d.case_status ||
                            get(d.case_status, 'case', 'status') ||
                            pickByKeywords(flat, 'case_status') ||
                            pickByKeywords(flat, 'nature', 'disposal') ||
                            pickByKeywords(flat, 'disposal', 'nature');

            var caseStatusVal = isValidStatusText(rawStatus) ? rawStatus : null;

            if (!caseStatusVal) {
                caseStatusVal = (decisionDateVal || currentStageText.includes('disposed')) ? 'Disposed' : 'Pending';
            }

            var statusElem = document.getElementById('live-case-status');
            if (statusElem && caseStatusVal) {
                statusElem.innerText = caseStatusVal;
                var lower = caseStatusVal.toLowerCase();
                if (lower.includes('pending') || lower.includes('active')) {
                    statusElem.className = 'fw-semibold text-primary';
                } else if (lower.includes('disposed') || lower.includes('decided')) {
                    statusElem.className = 'fw-semibold text-success';
                } else {
                    statusElem.className = 'fw-semibold text-dark';
                }
            }

            // ── Transfer Details ──────────────────────────────────────
            var transferEst = get(d.transfer_est, 'transfer') ||
                              pickByKeywords(flat, 'transfer', 'est') ||
                              pickByKeywords(flat, 'from', 'est');
            if (transferEst) {
                var tb = document.getElementById('live-transfer-block');
                if (tb) tb.classList.remove('d-none');
                setElem('live-transfer-est', transferEst);
                setElem('live-transfer-date',
                    get(d.transfer_date, 'transfer', 'date') ||
                    pickByKeywords(flat, 'xfer', 'date')
                );
                setElem('live-transfer-cino',
                    get(d.transfer_cino, 'transfer', 'cino') ||
                    pickByKeywords(flat, 'prev', 'cino') ||
                    pickByKeywords(flat, 'from', 'cino')
                );
            }

            // ── Extract & Render Hearing History & Orders ─────────────
            // Primary: use server-extracted arrays (controller does exact key matching)
            var historyItems = (json.history && json.history.length > 0)
                ? json.history
                : collectAllArrays(json.raw).historyList;

            var orderItems = (json.orders && json.orders.length > 0)
                ? json.orders.slice()
                : collectAllArrays(json.raw).ordersList.slice();

            console.log('Hearing history items (from server):', historyItems);
            console.log('Order items (from server):', orderItems);

            renderHearingHistory(historyItems);
            renderOrdersAndJudgments(orderItems);

            // ── Extract & Render IA Filings and Processes ─────────────
            var iaItems = (json.ia_filings && json.ia_filings.length > 0)
                ? json.ia_filings
                : (json.raw && json.raw.iafiling ? Object.values(json.raw.iafiling) : []);
            renderIaFilings(iaItems);

            var processItems = (json.processes && json.processes.length > 0)
                ? json.processes
                : (json.raw && json.raw.processes ? Object.values(json.raw.processes) : []);
            renderProcesses(processItems);

            // Save full District Court dataset for modal and full-data pane
            window.lastDcData = {
                data: d,
                raw: json.raw,
                history: historyItems,
                orders: orderItems,
                ia_filings: iaItems,
                processes: processItems,
                cnr: activeCnr || cnr
            };
            if (typeof renderFullCaseDetails === 'function') {
                renderFullCaseDetails(window.lastDcData, 'DC');
            }

            // If orderItems is empty, query dedicated NAPIX Orders API as fallback
            if (orderItems.length === 0) {
                fetch('/ECourts/GetOrders?cnrNumber=' + encodeURIComponent(cnr) + (window.lastCaseId ? '&caseId=' + window.lastCaseId : ''))
                    .then(function(r) { return r.json(); })
                    .then(function(oJson) {
                        if (oJson.success && oJson.data) {
                            var dedicatedArrays = collectAllArrays(oJson.data);
                            if (dedicatedArrays.ordersList.length > 0) {
                                renderOrdersAndJudgments(dedicatedArrays.ordersList);
                                if (window.lastDcData) window.lastDcData.orders = dedicatedArrays.ordersList;
                            }
                        }
                    })
                    .catch(function(err) {
                        console.warn('Dedicated orders fetch warning:', err);
                    });
            }
        })
        .catch(function(err) {
            console.error('CNR fetch error:', err);
            if (msgElem) msgElem.innerHTML = '<span class="text-danger fw-bold"><i class="bi bi-wifi-off me-1"></i> eCourts Gateway offline / Not connected</span>';
            ['live-first-date', 'live-next-date', 'live-stage', 'live-case-status', 'live-filing-date', 'live-decision-date', 'live-court-judge', 'live-pet-adv', 'live-respondent', 'live-extra-party'].forEach(function(id) {
                var el = document.getElementById(id);
                if (el) el.innerText = '—';
            });
            renderHearingHistory([]);
            renderOrdersAndJudgments([]);
            renderIaFilings([]);
            renderProcesses([]);
        });
}

/* ────────────────────────────────────────────────────
   High Court MFA: Live Sync & Dossier Rendering
──────────────────────────────────────────────────── */

function renderMfaHearingHistory(items, appealId) {
    var countBadge = document.getElementById('tabCountHistory_' + appealId);
    var countEl = document.getElementById('count_history_' + appealId);
    if (countBadge) countBadge.innerText = items ? items.length : 0;
    if (countEl) countEl.innerText = items ? items.length : 0;

    var tbody = document.getElementById('tbodyHistory_' + appealId);
    if (!tbody) return;

    if (!items || items.length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4"><i class="bi bi-info-circle me-1"></i> No hearing history records returned by eCourts Gateway.</td></tr>';
        return;
    }

    var html = '';
    items.forEach(function(item, idx) {
        var rawBDate = getObjProp(item, 'business_date', 'date', 'cause_date', 'dt_business', 'srno_date') || '';
        var rawHDate = getObjProp(item, 'hearing_date', 'next_date', 'next_hearing_date') || '';
        var validDate = extractValidDate(rawBDate) || extractValidDate(rawHDate) || rawBDate || rawHDate || '—';
        var dt = validDate;
        var nextDt = (rawHDate && rawHDate !== rawBDate && rawHDate !== '—' && !rawHDate.toLowerCase().includes('not given'))
                     ? rawHDate
                     : (getObjProp(item, 'next_date', 'next_hearing_date') || '');
        var purpose = getObjProp(item, 'purpose_name', 'stage', 'next_purpose', 'purposeName', 'purpose_of_listing') || '—';
        var judge = getObjProp(item, 'judge', 'desgname', 'judge_name', 'court_no_judge', 'presiding_officer') || '—';
        // Exact key match for 'business' to avoid fuzzy match returning business_date
        var itemLower = Object.keys(item).reduce(function(m2, k2) { m2[k2.toLowerCase()] = item[k2]; return m2; }, {});
        var mfaBizKeys = ['business', 'roznama', 'proceedings', 'court_business', 'business_details', 'short_order'];
        var mfaBizRaw = null;
        for (var mki = 0; mki < mfaBizKeys.length; mki++) {
            var mv = itemLower[mfaBizKeys[mki]];
            if (mv && typeof mv === 'string' && mv.trim().length > 5 && !/^(null|undefined|n\/a|na|none|—|--)$/i.test(mv.trim()) && !/^\d{2,4}[\-\/]/.test(mv.trim())) {
                mfaBizRaw = mv.trim(); break;
            }
        }
        var business = mfaBizRaw || '—';

        var badgeClass = 'bg-primary-subtle text-primary border border-primary-subtle';
        var pUpper = purpose.toUpperCase();
        if (pUpper.includes('DISPOSED') || pUpper.includes('DECIDED') || pUpper.includes('CLOSED')) {
            badgeClass = 'bg-success text-white shadow-sm';
        } else if (pUpper.includes('HEARING') || pUpper.includes('ARGUMENT') || pUpper.includes('PROCEEDING')) {
            badgeClass = 'bg-primary text-white shadow-sm';
        } else if (pUpper.includes('NOTICE') || pUpper.includes('SUMMONS') || pUpper.includes('STEPS')) {
            badgeClass = 'bg-warning text-dark shadow-sm';
        }

        var queryParamDate = (validDate !== '—') ? validDate : '';
        var dtLink = '<a href="javascript:void(0)" onclick="openHearingDetailsModal(\'HC\', ' + idx + ', \'' + appealId + '\', \'' + queryParamDate + '\')" class="text-primary text-decoration-none fw-bold" title="Click to view eCourts Daily Status for ' + dt + '"><i class="bi bi-box-arrow-up-right me-1 extra-small"></i>' + dt + '</a>';

        var mfaLiveDailyBtn = '<button type="button" onclick="openHearingDetailsModal(\'HC\', ' + idx + ', \'' + appealId + '\', \'' + queryParamDate + '\')" class="btn btn-sm btn-primary py-0 px-2 rounded-pill extra-small shadow-sm mt-1" title="View Official eCourts Daily Status for ' + dt + '">' +
            '<i class="bi bi-journal-text me-1"></i>Daily Status</button>';

        html += '<tr class="align-middle">' +
            '<td class="fw-bold text-muted py-2 px-3">' + (idx + 1) + '</td>' +
            '<td class="fw-bold text-dark py-2 px-3"><i class="bi bi-calendar-event text-primary me-1"></i>' + dtLink + (nextDt && nextDt !== dt && nextDt !== '—' ? '<br><small class="text-muted fw-normal">Next: ' + nextDt + '</small>' : '') + '</td>' +
            '<td class="py-2 px-3"><span class="badge ' + badgeClass + ' px-2 py-1">' + purpose + '</span></td>' +
            '<td class="py-2 px-3 text-secondary small"><i class="bi bi-person-fill me-1 text-muted"></i>' + judge + '</td>' +
            '<td class="py-2 px-3 text-secondary extra-small">' + (business || '<span class="text-muted">—</span>') + '<div class="mt-1">' + mfaLiveDailyBtn + '</div></td>' +
            '</tr>';
    });
    tbody.innerHTML = html;
}

function renderMfaOrders(items, appealId, cnr) {
    var countBadge = document.getElementById('tabCountOrders_' + appealId);
    var countEl = document.getElementById('count_orders_' + appealId);
    if (countBadge) countBadge.innerText = items ? items.length : 0;
    if (countEl) countEl.innerText = items ? items.length : 0;

    var tbody = document.getElementById('tbodyOrders_' + appealId);
    if (!tbody) return;

    if (!items || items.length === 0) {
        var portalUrl = (cnr && cnr.toUpperCase().startsWith('KAHC'))
            ? 'https://hcservices.ecourts.gov.in/hcservices/main.php'
            : 'https://njdg.ecourts.gov.in/njdgnew/index.php';

        tbody.innerHTML = '<tr>' +
            '<td colspan="5" class="text-center py-4 bg-light rounded-3">' +
            '<div class="text-muted mb-2"><i class="bi bi-info-circle text-warning fs-4 me-1"></i> No digital PDF order copy uploaded by court clerk on e-Courts NJDG gateway for CNR <strong>' + (cnr || '—') + '</strong>.</div>' +
            '<div class="d-flex align-items-center justify-content-center gap-2">' +
            '<a href="' + portalUrl + '" target="_blank" class="btn btn-sm btn-outline-danger rounded-pill px-3 shadow-sm fw-bold">' +
            '<i class="bi bi-box-arrow-up-right me-1"></i> Check High Court Portal (New Tab)' +
            '</a>' +
            '</div>' +
            '</td>' +
            '</tr>';
        return;
    }

    var html = '';
    items.forEach(function(item, idx) {
        var dt = getObjProp(item, 'order_date', 'orderDate', 'date', 'decision_date') || '—';
        var type = getObjProp(item, 'order_type', 'order_details', 'order_number', 'title', 'order_no') || 'Order / Judgment';
        var orderNo = getObjProp(item, 'order_number', 'order_no') || (idx + 1);
        var judge = getObjProp(item, 'judge', 'desgname', 'judge_name', 'court_no_judge') || '—';
        var pdfUrl = getObjProp(item, 'pdf_path', 'pdf_url', 'display_pdf_url', 'order_pdf', 'order_link', 'link', 'pdf', 'order_copy') || '';

        var targetUrl = '/ECourts/ViewOrderPdf?cnr=' + encodeURIComponent(cnr || '') + '&title=' + encodeURIComponent(type) + '&date=' + encodeURIComponent(dt) + '&orderNo=' + encodeURIComponent(orderNo);
        if (pdfUrl && !pdfUrl.startsWith('/ECourts/ViewOrderPdf')) {
            targetUrl = '/ECourts/ViewOrderPdf?url=' + encodeURIComponent(pdfUrl) + '&cnr=' + encodeURIComponent(cnr || '') + '&title=' + encodeURIComponent(type) + '&date=' + encodeURIComponent(dt) + '&orderNo=' + encodeURIComponent(orderNo);
        }

        var actionBtn = '<div class="d-inline-flex gap-1 align-items-center">' +
            '<button type="button" class="btn btn-sm btn-danger py-1 px-3 fw-bold text-white shadow-sm rounded-pill" onclick="openOrderInModal(\'' + targetUrl.replace(/'/g, "\\'") + '\', \'' + type.replace(/'/g, "\\'") + '\', \'' + (cnr || '') + '\', \'' + dt + '\', null, \'' + orderNo + '\')">' +
            '<i class="bi bi-file-pdf-fill me-1"></i> View Document' +
            '</button>' +
            '<a href="/ECourts/DownloadOrderPdf?pdfPath=' + encodeURIComponent(pdfUrl || '') + '&cnr=' + encodeURIComponent(cnr || '') + '&orderNo=' + encodeURIComponent(orderNo) + '&date=' + encodeURIComponent(dt) + '" target="_blank" class="btn btn-sm btn-outline-secondary py-1 px-2 rounded-pill shadow-sm" title="Download Order PDF"><i class="bi bi-download"></i></a>' +
            '</div>';

        html += '<tr class="align-middle">' +
            '<td class="fw-bold text-muted py-2 px-3">' + (idx + 1) + '</td>' +
            '<td class="fw-bold text-dark py-2 px-3"><i class="bi bi-calendar-check text-danger me-1"></i>' + dt + '</td>' +
            '<td class="py-2 px-3"><div class="fw-bold text-dark"><i class="bi bi-file-earmark-text text-danger me-1"></i>' + type + '</div></td>' +
            '<td class="py-2 px-3 text-secondary small"><i class="bi bi-bank me-1 text-muted"></i>' + judge + '</td>' +
            '<td class="py-2 px-3 text-end">' + actionBtn + '</td>' +
            '</tr>';
    });
    tbody.innerHTML = html;
}

function renderMfaIaFilings(items, appealId) {
    var countBadge = document.getElementById('tabCountIA_' + appealId);
    if (countBadge) countBadge.innerText = items ? items.length : 0;

    var tbody = document.getElementById('tbodyIA_' + appealId);
    if (!tbody) return;

    if (!items || items.length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4"><i class="bi bi-info-circle me-1"></i> No interim applications registered on e-Courts gateway.</td></tr>';
        return;
    }

    var html = '';
    items.forEach(function(item, idx) {
        var no = getObjProp(item, 'ia_number', 'ianumber', 'ia_no', 'sno') || ('IA No. ' + (idx + 1));
        var pet = getObjProp(item, 'ia_pet_name', 'party_name', 'pet_name', 'applicant') || '—';
        var dt = getObjProp(item, 'date_of_filing', 'filing_date', 'dt_filing', 'date') || '—';
        var pendDisp = (getObjProp(item, 'ia_pend_disp', 'status', 'pend_disp') || 'P').toUpperCase();
        var statusBadge = (pendDisp === 'D' || pendDisp.includes('DISPOSED'))
            ? '<span class="badge bg-success-subtle text-success border border-success px-2 py-1">Disposed</span>'
            : '<span class="badge bg-warning-subtle text-warning border border-warning px-2 py-1">Pending</span>';

        html += '<tr class="align-middle">' +
            '<td class="fw-bold text-muted py-2 px-3">' + (idx + 1) + '</td>' +
            '<td class="py-2 px-3"><div class="fw-semibold text-dark">' + no + '</div></td>' +
            '<td class="py-2 px-3 small text-secondary">' + pet + '</td>' +
            '<td class="py-2 px-3 text-muted small">' + dt + '</td>' +
            '<td class="py-2 px-3 text-center">' + statusBadge + '</td>' +
            '</tr>';
    });
    tbody.innerHTML = html;
}

function fetchLiveMfaDetails(cnr, caseId, appealId, isAuto) {
    if (!caseId) {
        caseId = window.currentCaseId || window.lastCaseId || null;
    }
    if (!appealId) {
        var firstInput = document.querySelector('[id^="cnrInput_"]');
        if (firstInput) {
            appealId = firstInput.id.replace('cnrInput_', '');
        }
    }
    if (!appealId) return;

    var cnrInput = document.getElementById('cnrInput_' + appealId);
    if (!cnr && cnrInput) {
        cnr = cnrInput.value.trim().toUpperCase();
    }
    if (!cnr || cnr.length < 12) {
        if (!isAuto) alert('Please enter a valid High Court CNR number (e.g. KAHC020060752019).');
        return;
    }

    if (cnrInput && (!cnrInput.value || cnrInput.value.trim().length === 0)) {
        cnrInput.value = cnr;
    }

    var btn = document.getElementById('btnRefreshMfa_' + appealId);
    var syncBtn = cnrInput ? cnrInput.parentElement.querySelector('button') : null;
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<i class="bi bi-arrow-repeat spin me-1"></i> Syncing...';
    }
    if (syncBtn) {
        syncBtn.disabled = true;
        syncBtn.innerHTML = '<i class="bi bi-arrow-repeat spin me-1"></i> Syncing...';
    }

    var updatedEl = document.getElementById('live_updated_' + appealId);
    if (updatedEl) {
        updatedEl.className = 'badge bg-primary-subtle text-primary border px-2 py-1 extra-small';
        updatedEl.innerHTML = '<i class="bi bi-broadcast me-1"></i> Syncing with e-Courts...';
    }

    var url = '/ECourts/GetCnrDetails?cnrNumber=' + encodeURIComponent(cnr) + '&isHighCourt=true';
    if (caseId) url += '&caseId=' + encodeURIComponent(caseId);
    if (appealId) url += '&appealId=' + encodeURIComponent(appealId);

    fetch(url)
        .then(function(r) { return r.json(); })
        .then(function(res) {
            if (res && res.success) {
                var d = res.data || {};
                var flat = {};
                if (res.raw) flattenObj(res.raw, '', flat);
                if (d) flattenObj(d, '', flat);

                // 1. CNR Badge
                var cnrBadge = document.getElementById('live_cnr_badge_' + appealId);
                if (cnrBadge) {
                    cnrBadge.className = 'badge bg-primary font-monospace fs-6 px-3 py-2 shadow-sm';
                    cnrBadge.innerHTML = '<i class="bi bi-broadcast me-1"></i> CNR: ' + cnr;
                }

                // 2. Updated Badge
                var now = new Date();
                var timeStr = now.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' }) + ' ' +
                              now.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' });
                if (updatedEl) {
                    updatedEl.className = 'badge bg-success-subtle text-success border border-success px-2 py-1 extra-small';
                    updatedEl.innerHTML = '<i class="bi bi-check-circle-fill me-1"></i> Live: ' + timeStr;
                }

                // 3. MFA Case No & Year
                var caseNo = d.case_number || pickByKeywords(flat, 'reg_no') || pickByKeywords(flat, 'case_number') || pickByKeywords(flat, 'case_no');
                var mfaNoEl = document.getElementById('live_mfa_no_' + appealId);
                if (mfaNoEl && caseNo) {
                    var regDateVal = d.registration_date || pickByKeywords(flat, 'dt_regis') || pickByKeywords(flat, 'registration_date');
                    var yr = '';
                    if (regDateVal && regDateVal.length >= 4) {
                        var parsedDate = parseAnyDate(regDateVal);
                        yr = parsedDate ? parsedDate.getFullYear() : regDateVal.slice(-4);
                    }
                    var ct = d.case_type || pickByKeywords(flat, 'type_name') || '';
                    if (!ct && String(appealId).toLowerCase().includes('wa')) ct = 'WA';
                    else if (!ct && String(appealId).toLowerCase().includes('wp')) ct = 'WP';
                    else if (!ct) ct = 'MFA';
                    mfaNoEl.innerText = ct + ' ' + caseNo + (yr ? ' / ' + yr : '');
                }

                // 4. High Court Bench
                var bench = d.establishment_name || pickByKeywords(flat, 'establishment_name') || pickByKeywords(flat, 'court_est_name') || pickByKeywords(flat, 'bench');
                if (bench) {
                    var benchEl = document.getElementById('live_bench_' + appealId);
                    if (benchEl) {
                        benchEl.innerText = bench;
                        benchEl.setAttribute('title', bench);
                    }
                }

                // 5. Next Hearing Date
                var nextDate = d.next_hearing_date || pickByKeywords(flat, 'next_date') || pickByKeywords(flat, 'next_hearing_date') || pickByKeywords(flat, 'next_hearing');
                if (nextDate && nextDate !== '—') {
                    var hElem = document.getElementById('live_hearing_' + appealId);
                    if (hElem) {
                        hElem.innerHTML = '<a href="javascript:void(0)" onclick="openHearingDetailsModal(\'HC\', -1, \'' + appealId + '\', \'' + nextDate + '\')" class="text-primary text-decoration-none fw-bold" title="Click to view eCourts Daily Status for this hearing"><i class="bi bi-calendar-event me-1 text-primary"></i>' + nextDate + ' <span class="badge bg-primary-subtle text-primary extra-small border ms-1"><i class="bi bi-journal-text me-1"></i>Daily Status</span></a>';
                    } else {
                        setElem('live_hearing_' + appealId, nextDate);
                    }
                }

                // 6. Case Stage
                var stage = d.stage || pickByKeywords(flat, 'purpose_name') || pickByKeywords(flat, 'court_stage') || pickByKeywords(flat, 'stage') || '—';
                var stageEl = document.getElementById('live_stage_' + appealId);
                if (stageEl && stage && stage !== '—') {
                    stageEl.innerText = stage;
                    var su = stage.toUpperCase();
                    if (su.includes('DISPOSED') || su.includes('DECIDED') || su.includes('CLOSED')) {
                        stageEl.className = 'badge bg-success text-white shadow-sm px-2 py-1';
                    } else if (su.includes('HEARING') || su.includes('ARGUMENT') || su.includes('ADMISSION')) {
                        stageEl.className = 'badge bg-primary text-white shadow-sm px-2 py-1';
                    } else {
                        stageEl.className = 'badge bg-info text-dark shadow-sm px-2 py-1';
                    }
                }

                // 7. Case Status
                var status = d.case_status || (stage && stage.toUpperCase().includes('DISPOSED') ? 'Disposed' : 'Pending');
                var statusEl = document.getElementById('live_status_' + appealId);
                if (statusEl) {
                    statusEl.innerText = status;
                    if (status.toUpperCase().includes('PENDING') || status.toUpperCase().includes('ACTIVE')) {
                        statusEl.className = 'fw-semibold text-primary';
                    } else if (status.toUpperCase().includes('DISPOSED') || status.toUpperCase().includes('DECIDED')) {
                        statusEl.className = 'fw-semibold text-success';
                    } else {
                        statusEl.className = 'fw-semibold text-dark';
                    }
                }

                // 8. Dates
                var filDate = d.filing_date || pickByKeywords(flat, 'date_of_filing') || pickByKeywords(flat, 'filing_date') || pickByKeywords(flat, 'filing_dt');
                setElem('live_filing_date_' + appealId, filDate || '—');

                var regDate = d.registration_date || pickByKeywords(flat, 'dt_regis') || pickByKeywords(flat, 'registration_date') || pickByKeywords(flat, 'reg_date');
                setElem('live_reg_date_' + appealId, regDate || '—');

                var decDate = d.decision_date || pickByKeywords(flat, 'date_of_decision') || pickByKeywords(flat, 'decision_date') || pickByKeywords(flat, 'disposal_date');
                setElem('live_decision_date_' + appealId, decDate || '—');

                // 9. Coram / Judges
                var judgeVal = d.court_no_judge || d.judge || pickByKeywords(flat, 'coram') || pickByKeywords(flat, 'bench_coram') || pickByKeywords(flat, 'judge') || pickByKeywords(flat, 'court_no_judge') || pickByKeywords(flat, 'desgname');
                setElem('live_judge_' + appealId, judgeVal || '—');

                // 10. Case Type & Classification
                var caseTypeVal = d.case_type || pickByKeywords(flat, 'type_name') || '—';
                var cat = pickByKeywords(flat, 'category');
                var subCat = pickByKeywords(flat, 'sub_category');
                if (cat && caseTypeVal !== '—') caseTypeVal += ' — ' + cat;
                if (subCat && caseTypeVal !== '—') caseTypeVal += ' (' + subCat + ')';
                setElem('live_casetype_' + appealId, caseTypeVal);

                // 11. Parties & Advocates
                var pet = d.petitioner || pickByKeywords(flat, 'pet_name') || pickByKeywords(flat, 'petitioner') || pickByKeywords(flat, 'appellant');
                setElem('live_petitioner_' + appealId, pet || '—');

                var petAdv = d.petitioner_advocate || pickByKeywords(flat, 'pet_adv') || pickByKeywords(flat, 'petitioner_advocate');
                setElem('live_pet_adv_' + appealId, petAdv || '—');

                var resParty = d.respondent || pickByKeywords(flat, 'res_name') || pickByKeywords(flat, 'respondent');
                setElem('live_respondent_' + appealId, resParty || '—');

                var resAdv = d.respondent_advocate || pickByKeywords(flat, 'res_adv') || pickByKeywords(flat, 'respondent_advocate');
                setElem('live_res_adv_' + appealId, resAdv || '—');

                // 12. Full Case Details Tab Meta
                setElem('fullPetitioner_' + appealId, pet || '—');
                setElem('fullRespondent_' + appealId, resParty || '—');
                setElem('fullPetAdv_' + appealId, petAdv || '—');
                setElem('fullResAdv_' + appealId, resAdv || '—');
                setElem('fullCourt_' + appealId, judgeVal || bench || '—');
                setElem('fullFilingDate_' + appealId, filDate || '—');
                setElem('fullRegDate_' + appealId, regDate || '—');
                setElem('fullDisposalDate_' + appealId, decDate || '—');

                // 13. Hearing History
                var historyItems = (res.history && res.history.length > 0)
                    ? res.history
                    : collectAllArrays(res.raw).historyList;
                renderMfaHearingHistory(historyItems, appealId);

                // 14. Orders & Judgments
                var orderItems = (res.orders && res.orders.length > 0)
                    ? res.orders
                    : collectAllArrays(res.raw).ordersList;
                renderMfaOrders(orderItems, appealId, cnr);

                // 15. Interim Applications (I.A.)
                var iaItems = (res.ia_filings && res.ia_filings.length > 0)
                    ? res.ia_filings
                    : (res.raw && res.raw.iafiling ? Object.values(res.raw.iafiling) : []);
                renderMfaIaFilings(iaItems, appealId);

                // Store MFA data for modal and dossier inspection
                window.lastMfaData = window.lastMfaData || {};
                window.lastMfaData[appealId] = {
                    data: d,
                    raw: res.raw,
                    history: historyItems,
                    orders: orderItems,
                    ia_filings: iaItems,
                    cnr: cnr,
                    appealId: appealId,
                    case_number: caseNo || d.case_number || pickByKeywords(flat, 'case_number', 'case_no', 'reg_no') || '',
                    petitioner: pet || d.petitioner || pickByKeywords(flat, 'pet_name', 'petitioner', 'appellant') || '—',
                    respondent: resParty || d.respondent || pickByKeywords(flat, 'res_name', 'respondent') || '—',
                    court_judge: judgeVal || bench || d.court_no_judge || d.judge || pickByKeywords(flat, 'court_no_judge', 'judge', 'coram', 'desgname') || 'High Court Bench'
                };

                var card = document.getElementById('ecourtsLiveCard_' + appealId);
                if (card) card.style.display = '';

                if (!isAuto) {
                    alert('High Court MFA e-Courts live data & dossier synced successfully!');
                }
            } else {
                var msg = (res && res.message) ? res.message : 'Could not fetch live status.';
                if (updatedEl) {
                    updatedEl.className = 'badge bg-warning-subtle text-warning border border-warning px-2 py-1 extra-small';
                    updatedEl.innerHTML = '<i class="bi bi-exclamation-triangle-fill me-1"></i> ' + msg;
                }
                if (!isAuto) alert(msg);
            }
        })
        .catch(function(err) {
            console.error('MFA CNR fetch error:', err);
            if (updatedEl) {
                updatedEl.className = 'badge bg-danger-subtle text-danger border border-danger px-2 py-1 extra-small';
                updatedEl.innerHTML = '<i class="bi bi-wifi-off me-1"></i> Gateway Offline / Error';
            }
            if (!isAuto) alert('Network error: ' + err);
        })
        .finally(function() {
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = '<i class="bi bi-arrow-repeat me-1"></i> Fetch Live Data';
            }
            if (syncBtn) {
                syncBtn.disabled = false;
                syncBtn.innerHTML = '<i class="bi bi-cloud-arrow-down-fill me-1"></i> Sync Live';
            }
        });
}

function syncMfaDetails(appealId, isAuto) {
    var cnr = null;
    var cnrInput = document.getElementById('cnrInput_' + appealId);
    if (cnrInput) {
        cnr = cnrInput.value.trim().toUpperCase();
    }
    var caseId = window.currentCaseId || window.lastCaseId || null;
    fetchLiveMfaDetails(cnr, caseId, appealId, isAuto);
}
window.syncMfaDetails = syncMfaDetails;
window.syncAppealDetails = syncMfaDetails;

// Auto-sync all High Court MFA inputs on page load
function autoInitAllMfaSync() {
    var cnrInputs = document.querySelectorAll('[id^="cnrInput_"]');
    cnrInputs.forEach(function(input) {
        var appealId = input.id.replace('cnrInput_', '');
        var val = (input.value || '').trim();
        if (val.length >= 12) {
            fetchLiveMfaDetails(val.toUpperCase(), window.currentCaseId || null, appealId, true);
        }
    });
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function() {
        setTimeout(autoInitAllMfaSync, 300);
    });
} else {
    setTimeout(autoInitAllMfaSync, 100);
}

/* ────────────────────────────────────────────────────
   Inline Auto-Discover and Link CNR
──────────────────────────────────────────────────── */
function autoLinkCnrInline(caseId, module) {
    var btn = document.getElementById('btn-autolink-cnr');
    var statusSpan = document.getElementById('autolink-status-msg');
    var oldText = btn ? btn.innerHTML : '';
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status"></span>Searching e-Courts...';
    }
    if (statusSpan) {
        statusSpan.innerHTML = '<span class="text-primary small"><i class="bi bi-hourglass-split me-1"></i>Discovering CNR via Case No & Parties...</span>';
    }

    var postData = new URLSearchParams();
    postData.append('caseId', caseId);
    if (module) postData.append('module', module);

    fetch('/ECourts/AutoLinkCnrInline', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: postData.toString()
    })
    .then(function(r) { return r.json(); })
    .then(function(data) {
        if (data.success && data.cnr) {
            if (statusSpan) {
                statusSpan.innerHTML = '<span class="text-success small fw-bold"><i class="bi bi-check-circle-fill me-1"></i>Linked CNR: ' + data.cnr + (data.strategy ? ' (' + data.strategy + ')' : '') + '</span>';
            }
            if (btn) {
                btn.className = 'btn btn-sm btn-success rounded-pill px-3';
                btn.innerHTML = '<i class="bi bi-check-lg me-1"></i>CNR Linked!';
            }
            setElem('lbl-cnr-number', data.cnr);
            setElem('cnr_badge', data.cnr);
            var badgeEl = document.getElementById('details-cnr-badge');
            if (badgeEl) {
                badgeEl.innerHTML = '<span class="badge bg-success font-monospace fs-6 px-3 py-2 shadow-sm"><i class="bi bi-check-circle-fill me-1"></i> CNR: ' + data.cnr + '</span>';
            }
            setTimeout(function() {
                fetchLiveCnrDetails(data.cnr, caseId);
            }, 500);
        } else {
            if (statusSpan) {
                statusSpan.innerHTML = '<span class="text-danger small"><i class="bi bi-exclamation-triangle me-1"></i>' + (data.message || 'CNR could not be auto-discovered') + '</span>';
            }
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = oldText || '<i class="bi bi-search me-1"></i>Auto-Discover & Link CNR';
            }
        }
    })
    .catch(function(err) {
        if (statusSpan) {
            statusSpan.innerHTML = '<span class="text-danger small"><i class="bi bi-x-circle me-1"></i>Error connecting to eCourts service</span>';
        }
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = oldText || '<i class="bi bi-search me-1"></i>Auto-Discover & Link CNR';
        }
    });
}

/* ────────────────────────────────────────────────────
   Tab Switching Reliability Guarantee (Bootstrap 5 Delegation)
──────────────────────────────────────────────────── */
document.addEventListener('click', function(e) {
    var tabBtn = e.target.closest('#ecourtsTab button, .dossier-tab-nav button, button[data-bs-toggle="tab"], button[data-bs-toggle="pill"]');
    if (!tabBtn) return;

    var targetSel = tabBtn.getAttribute('data-bs-target') || tabBtn.getAttribute('href');
    if (!targetSel) return;

    if (typeof bootstrap !== 'undefined' && bootstrap.Tab) {
        try {
            var tabInst = bootstrap.Tab.getInstance(tabBtn) || new bootstrap.Tab(tabBtn);
            tabInst.show();
            return;
        } catch (err) {
            console.warn('Bootstrap Tab API fallback:', err);
        }
    }

    var nav = tabBtn.closest('.nav');
    if (nav) {
        nav.querySelectorAll('.nav-link').forEach(function(btn) {
            btn.classList.remove('active');
            btn.setAttribute('aria-selected', 'false');
        });
    }
    tabBtn.classList.add('active');
    tabBtn.setAttribute('aria-selected', 'true');

    var pane = document.querySelector(targetSel);
    if (pane) {
        var parentContent = pane.closest('.tab-content');
        if (parentContent) {
            parentContent.querySelectorAll('.tab-pane').forEach(function(p) {
                p.classList.remove('show', 'active');
            });
        }
        pane.classList.add('show', 'active');
    }
});

/* ────────────────────────────────────────────────────
   Inline Tab 5 Renderer: Full Case Details Dossier
──────────────────────────────────────────────────── */
function renderFullCaseDetails(dcData, source) {
    var container = document.getElementById('live-fulldata-container');
    if (!container || !dcData) return;

    var d = dcData.data || {};
    var raw = dcData.raw || {};
    var history = dcData.history || [];
    var orders = dcData.orders || [];
    var ia_filings = dcData.ia_filings || [];
    var processes = dcData.processes || [];

    var cnr = dcData.cnr || d.cnr_number || d.cnr || window.lastCnr || '—';
    var pet = d.petitioner || '—';
    var petAdv = d.petitioner_advocate || '—';
    var res = d.respondent || '—';
    var resAdv = d.respondent_advocate || '—';
    var courtJudge = d.court_no_judge || d.judge || '—';
    var filDate = d.filing_date || '—';
    var regDate = d.registration_date || '—';
    var decDate = d.decision_date || '—';
    var nextDate = d.next_hearing_date || '—';
    var stage = d.stage || '—';
    var status = d.case_status || 'Pending';

    var html =
        '<div class="p-3">' +
        '  <div class="d-flex flex-wrap justify-content-between align-items-center mb-3 pb-2 border-bottom">' +
        '    <div>' +
        '      <h6 class="fw-bold text-dark mb-0"><i class="bi bi-file-earmark-ruled-fill text-primary me-2"></i>Complete Case Dossier (e-Courts NAPIX)</h6>' +
        '      <span class="text-muted extra-small">CNR: <strong class="font-monospace text-primary">' + cnr + '</strong> | Court: ' + courtJudge + '</span>' +
        '    </div>' +
        '  </div>' +

        '  <div class="row g-3 mb-4">' +
        '    <div class="col-md-6 col-lg-3">' +
        '      <div class="p-3 bg-light rounded-3 border h-100 shadow-xs">' +
        '        <div class="extra-small text-uppercase text-muted fw-bold mb-1">Registration & Filing</div>' +
        '        <div class="small">Reg Date: <strong class="text-dark">' + regDate + '</strong></div>' +
        '        <div class="small">Filing Date: <strong class="text-dark">' + filDate + '</strong></div>' +
        '      </div>' +
        '    </div>' +
        '    <div class="col-md-6 col-lg-3">' +
        '      <div class="p-3 bg-light rounded-3 border h-100 shadow-xs">' +
        '        <div class="extra-small text-uppercase text-muted fw-bold mb-1">Status & Next Date</div>' +
        '        <div class="small">Status: <strong class="text-primary">' + status + '</strong></div>' +
        '        <div class="small">Next Date: <strong class="text-success fw-bold">' + nextDate + '</strong></div>' +
        '      </div>' +
        '    </div>' +
        '    <div class="col-md-6 col-lg-3">' +
        '      <div class="p-3 bg-light rounded-3 border h-100 shadow-xs">' +
        '        <div class="extra-small text-uppercase text-muted fw-bold mb-1">Petitioner vs Respondent</div>' +
        '        <div class="small text-truncate" title="' + pet + '">Pet: <strong class="text-dark">' + pet + '</strong></div>' +
        '        <div class="small text-truncate" title="' + res + '">Res: <strong class="text-dark">' + res + '</strong></div>' +
        '      </div>' +
        '    </div>' +
        '    <div class="col-md-6 col-lg-3">' +
        '      <div class="p-3 bg-light rounded-3 border h-100 shadow-xs">' +
        '        <div class="extra-small text-uppercase text-muted fw-bold mb-1">Records Summary</div>' +
        '        <div class="small"><span class="badge bg-primary me-1">' + history.length + ' Hearings</span> <span class="badge bg-danger me-1">' + orders.length + ' Orders</span> <span class="badge bg-warning text-dark me-1">' + ia_filings.length + ' IAs</span> <span class="badge bg-info text-dark">' + processes.length + ' Notices</span></div>' +
        '      </div>' +
        '    </div>' +
        '  </div>' +
        '</div>';

    container.innerHTML = html;
}

/* ────────────────────────────────────────────────────
   Interactive Modal 1: Complete Case Dossier (Full Data Modal)
──────────────────────────────────────────────────── */
function openFullECourtsModal(source, appealId) {
    source = (source || 'DC').toUpperCase();
    var modalElem = document.getElementById('ecourtsFullDataModal');
    if (!modalElem) {
        var modalHtml =
            '<div class="modal fade" id="ecourtsFullDataModal" tabindex="-1" aria-labelledby="ecourtsFullDataModalTitle" aria-hidden="true">' +
            '  <div class="modal-dialog modal-xl modal-dialog-centered modal-dialog-scrollable">' +
            '    <div class="modal-content border-0 shadow-lg">' +
            '      <div class="modal-header bg-dark text-white py-3 px-4">' +
            '        <div class="d-flex align-items-center gap-2">' +
            '          <i class="bi bi-shield-shaded text-warning fs-4"></i>' +
            '          <div>' +
            '            <h5 class="modal-title fw-bold mb-0" id="ecourtsFullDataModalTitle">e-Courts NAPIX Live Case Dossier</h5>' +
            '            <span class="text-light extra-small opacity-75" id="ecourtsFullDataSubtitle">National Judicial Data Grid (NJDG) Live Services</span>' +
            '          </div>' +
            '        </div>' +
            '        <div class="d-flex align-items-center gap-2">' +
            '          <span class="badge bg-primary font-monospace px-3 py-2 fs-6 shadow-sm" id="modalCnrBadge">—</span>' +
            '          <button type="button" class="btn btn-sm btn-outline-light py-1 px-2" title="Copy CNR Number" onclick="copyModalCnr()"><i class="bi bi-clipboard"></i></button>' +
            '          <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>' +
            '        </div>' +
            '      </div>' +
            '      <div class="modal-body p-0" id="ecourtsFullDataModalBody">' +
            '        <div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div><p class="mt-2 text-muted">Loading live case data...</p></div>' +
            '      </div>' +
            '      <div class="modal-footer bg-light py-2 px-4 justify-content-between">' +
            '        <span class="extra-small text-muted"><i class="bi bi-shield-check text-success me-1"></i> Official e-Courts NAPIX Live Data Feed</span>' +
            '        <button type="button" class="btn btn-sm btn-secondary rounded-pill px-4 fw-semibold" data-bs-dismiss="modal">Close</button>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';
        document.body.insertAdjacentHTML('beforeend', modalHtml);
        modalElem = document.getElementById('ecourtsFullDataModal');
    }

    var bodyElem = document.getElementById('ecourtsFullDataModalBody');
    var titleElem = document.getElementById('ecourtsFullDataModalTitle');
    var subElem = document.getElementById('ecourtsFullDataSubtitle');
    var badgeElem = document.getElementById('modalCnrBadge');

    // Show modal immediately with loading state if needed
    if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
        var bsModal = bootstrap.Modal.getInstance(modalElem) || new bootstrap.Modal(modalElem);
        bsModal.show();
    } else {
        $(modalElem).modal('show');
    }

    var dataBundle = null;
    if (source === 'HC' && appealId) {
        if (titleElem) titleElem.innerHTML = '<i class="bi bi-building me-2 text-warning"></i>High Court Appeal Dossier (MFA)';
        if (subElem) subElem.innerText = 'Karnataka High Court (Bengaluru / Dharwad / Kalaburagi)';
        if (window.lastMfaData && window.lastMfaData[appealId]) {
            dataBundle = window.lastMfaData[appealId];
        } else {
            // Auto-trigger sync if needed
            var cnrInput = document.getElementById('cnrInput_' + appealId);
            var mfaCnr = cnrInput ? cnrInput.value.trim().toUpperCase() : '';
            if (mfaCnr && mfaCnr.length >= 12) {
                syncMfaDetails(appealId, false);
            }
        }
    } else {
        if (titleElem) titleElem.innerHTML = '<i class="bi bi-bank2 me-2 text-warning"></i>District &amp; Sessions Court Case Dossier (MACT)';
        if (subElem) subElem.innerText = 'District & Taluka Court Complex (eCourts Services)';
        if (window.lastDcData) {
            dataBundle = window.lastDcData;
        } else if (window.lastLiveCnrData) {
            dataBundle = {
                data: window.lastLiveCnrData,
                raw: window.lastLiveCnrFull ? window.lastLiveCnrFull.raw : {},
                history: window.lastLiveCnrFull ? window.lastLiveCnrFull.history : [],
                orders: window.lastLiveCnrFull ? window.lastLiveCnrFull.orders : [],
                ia_filings: window.lastLiveCnrFull ? window.lastLiveCnrFull.ia_filings : [],
                processes: window.lastLiveCnrFull ? window.lastLiveCnrFull.processes : [],
                cnr: window.lastCnr
            };
        }
    }

    if (!dataBundle) {
        if (bodyElem) {
            bodyElem.innerHTML =
                '<div class="text-center py-5 px-4">' +
                '  <i class="bi bi-broadcast text-primary fs-1 mb-2"></i>' +
                '  <h5 class="fw-bold text-dark">Connecting to e-Courts Gateway...</h5>' +
                '  <p class="text-muted small">Live case data is currently syncing. Please wait a moment or click Refresh.</p>' +
                '  <div class="spinner-border text-primary my-2" role="status"></div>' +
                '</div>';
        }
        return;
    }

    renderModalContent(dataBundle, source, appealId);
}

function copyModalCnr() {
    var b = document.getElementById('modalCnrBadge');
    if (b) {
        var text = b.innerText.replace('CNR:', '').trim();
        navigator.clipboard.writeText(text);
        alert('CNR copied to clipboard: ' + text);
    }
}

function renderModalContent(bundle, source, appealId) {
    var bodyElem = document.getElementById('ecourtsFullDataModalBody');
    var badgeElem = document.getElementById('modalCnrBadge');
    if (!bodyElem) return;

    var d = bundle.data || {};
    var raw = bundle.raw || {};
    var history = bundle.history || [];
    var orders = bundle.orders || [];
    var ia_filings = bundle.ia_filings || [];
    var processes = bundle.processes || [];
    var cnr = bundle.cnr || d.cnr_number || d.cnr || '—';

    if (badgeElem) badgeElem.innerText = 'CNR: ' + cnr;

    var caseNo = d.case_number || (source === 'HC' ? 'MFA Appeal' : 'MVC Case');
    var stage = d.stage || '—';
    var status = d.case_status || 'Pending';
    var courtJudge = d.court_no_judge || d.judge || '—';
    var filDate = d.filing_date || '—';
    var regDate = d.registration_date || '—';
    var decDate = d.decision_date || '—';
    var nextDate = d.next_hearing_date || '—';

    var pet = d.petitioner || '—';
    var petAdv = d.petitioner_advocate || '—';
    var res = d.respondent || '—';
    var resAdv = d.respondent_advocate || '—';
    var extraParty = d.extra_party || '—';

    // Raw JSON string
    var rawJsonStr = '';
    try {
        rawJsonStr = JSON.stringify(raw && Object.keys(raw).length > 0 ? raw : d, null, 2);
    } catch (e) {
        rawJsonStr = 'Error formatting JSON payload';
    }

    // Generate Hearing Rows
    var histRows = '';
    if (history.length === 0) {
        histRows = '<tr><td colspan="5" class="text-center py-4 text-muted">No hearing history records found in gateway.</td></tr>';
    } else {
        history.forEach(function(h, idx) {
            var bDate = getObjProp(h, 'business_date', 'date', 'cause_date', 'dt_business', 'srno_date') || '—';
            var hDate = getObjProp(h, 'hearing_date', 'next_date', 'next_hearing_date', 'date_next_list') || '—';
            var pur = getObjProp(h, 'purpose_of_listing', 'purpose_name', 'stage', 'purpose', 'purpose_of_hearing') || '—';
            var jdg = getObjProp(h, 'judge', 'judge_name', 'court_no_judge', 'presiding_officer', 'desgname') || courtJudge;
            var bus = getObjProp(h, 'business', 'roznama', 'business_details', 'proceedings', 'court_business') || '—';

            histRows +=
                '<tr class="align-middle">' +
                '  <td class="fw-bold text-center text-muted small">' + (idx + 1) + '</td>' +
                '  <td class="small fw-bold text-dark"><a href="javascript:void(0)" onclick="openHearingDetailsModal(\'' + source + '\', ' + idx + ', \'' + (appealId || '') + '\')" class="text-primary text-decoration-none fw-bold" title="View Roznama details"><i class="bi bi-box-arrow-up-right me-1 extra-small"></i>' + (bDate !== '—' ? bDate : hDate) + '</a>' + (hDate && hDate !== bDate && hDate !== '—' ? '<div class="extra-small text-muted">Next: ' + hDate + '</div>' : '') + '</td>' +
                '  <td><span class="badge bg-secondary-subtle text-dark border px-2 py-1 small">' + pur + '</span></td>' +
                '  <td class="small text-secondary" style="max-width:200px;">' + jdg + '</td>' +
                '  <td class="extra-small text-dark text-break" style="max-width:320px;">' + (bus !== '—' ? bus : '<span class="text-muted">—</span>') + '</td>' +
                '</tr>';
        });
    }

    // Generate Order Rows
    var orderRows = '';
    if (orders.length === 0) {
        orderRows = '<tr><td colspan="5" class="text-center py-4 text-muted">No orders or judgments uploaded for this case.</td></tr>';
    } else {
        orders.forEach(function(o, idx) {
            var oNo = getObjProp(o, 'order_number', 'order_no', 'sr_no') || (idx + 1);
            var oDt = getObjProp(o, 'order_date', 'date', 'order_dt') || '—';
            var oTitle = getObjProp(o, 'order_details', 'details', 'order_type', 'title') || 'Court Order';
            var oJudge = getObjProp(o, 'judge_name', 'judge', 'court_no_judge') || courtJudge;
            var pdfUrl = getObjProp(o, 'order_pdf_url', 'pdf_url', 'pdf_path', 'url', 'pdf') || '';

            var safeTitle = oTitle.replace(/'/g, "\\'");
            var safePdfUrl = pdfUrl.replace(/'/g, "\\'");

            var actionBtn = '<div class="d-inline-flex gap-1">' +
                '<button type="button" class="btn btn-xs btn-danger text-white rounded-pill px-2 py-1 shadow-sm" onclick="openOrderInModal(\'' + safePdfUrl + '\', \'' + safeTitle + '\', \'' + cnr + '\', \'' + oDt + '\', null, \'' + oNo + '\')"><i class="bi bi-file-earmark-pdf me-1"></i>View PDF</button>' +
                '<a href="/ECourts/DownloadOrderPdf?pdfPath=' + encodeURIComponent(pdfUrl) + '&cnr=' + encodeURIComponent(cnr) + '&orderNo=' + encodeURIComponent(oNo) + '&date=' + encodeURIComponent(oDt) + '" target="_blank" class="btn btn-xs btn-outline-secondary rounded-pill px-2 py-1" title="Download"><i class="bi bi-download"></i></a>' +
                '</div>';

            orderRows +=
                '<tr class="align-middle">' +
                '  <td class="fw-bold text-center text-muted small">' + oNo + '</td>' +
                '  <td class="small fw-bold text-dark"><i class="bi bi-calendar-check text-danger me-1"></i>' + oDt + '</td>' +
                '  <td class="small fw-semibold text-dark"><i class="bi bi-file-earmark-text text-primary me-1"></i>' + oTitle + '</td>' +
                '  <td class="small text-secondary">' + oJudge + '</td>' +
                '  <td class="text-end">' + actionBtn + '</td>' +
                '</tr>';
        });
    }

    // Generate IA Rows
    var iaRows = '';
    if (ia_filings.length === 0) {
        iaRows = '<tr><td colspan="5" class="text-center py-4 text-muted">No Interim Applications (IA) found.</td></tr>';
    } else {
        ia_filings.forEach(function(ia, idx) {
            var iaNo = getObjProp(ia, 'ia_number', 'ia_no', 'no') || (idx + 1);
            var iaParty = getObjProp(ia, 'ia_pet_name', 'party_name', 'applicant') || '—';
            var iaDt = getObjProp(ia, 'date_of_filing', 'filing_date', 'date') || '—';
            var prayer = getObjProp(ia, 'ia_prayer', 'prayer', 'purpose') || '—';
            var iaStatus = getObjProp(ia, 'ia_pend_disp', 'status') || 'Pending';

            iaRows +=
                '<tr class="align-middle">' +
                '  <td class="fw-bold text-center text-muted small">' + (idx + 1) + '</td>' +
                '  <td class="small fw-bold text-dark">' + iaNo + '</td>' +
                '  <td class="small text-dark">' + iaParty + '</td>' +
                '  <td class="small text-muted">' + prayer + '</td>' +
                '  <td class="small text-secondary">' + iaDt + '</td>' +
                '</tr>';
        });
    }

    // Generate Process Rows
    var procRows = '';
    if (processes.length === 0) {
        procRows = '<tr><td colspan="5" class="text-center py-4 text-muted">No processes or notices registered.</td></tr>';
    } else {
        processes.forEach(function(p, idx) {
            var pId = getObjProp(p, 'process_id', 'id') || ('P-' + (idx + 1));
            var pDt = getObjProp(p, 'process_date', 'date') || '—';
            var pTitle = getObjProp(p, 'process_title', 'title') || 'Notice';
            var pParty = getObjProp(p, 'party_name', 'recipient') || '—';
            var rDt = getObjProp(p, 'return_date', 'next_date') || '—';

            procRows +=
                '<tr class="align-middle">' +
                '  <td class="fw-bold text-center text-muted small">' + (idx + 1) + '</td>' +
                '  <td class="small fw-bold text-primary font-monospace"><a href="javascript:void(0)" onclick="openProcessDetailsModal(\'DC\', ' + idx + ')" class="text-primary text-decoration-none fw-bold">' + pId + '</a></td>' +
                '  <td class="small text-secondary">' + pDt + '</td>' +
                '  <td class="small text-dark fw-semibold">' + pTitle + '</td>' +
                '  <td class="small text-secondary">' + pParty + '</td>' +
                '</tr>';
        });
    }

    var html =
        '<div class="p-4 bg-light-subtle border-bottom">' +
        '  <div class="row g-3">' +
        '    <div class="col-6 col-md-3">' +
        '      <div class="p-3 bg-white border rounded-3 shadow-xs h-100">' +
        '        <div class="extra-small text-uppercase text-muted fw-bold mb-1">Case Number &amp; CNR</div>' +
        '        <div class="fw-bold text-dark fs-6">' + caseNo + '</div>' +
        '        <div class="font-monospace text-primary small mt-1">' + cnr + '</div>' +
        '      </div>' +
        '    </div>' +
        '    <div class="col-6 col-md-3">' +
        '      <div class="p-3 bg-white border rounded-3 shadow-xs h-100">' +
        '        <div class="extra-small text-uppercase text-muted fw-bold mb-1">Status &amp; Stage</div>' +
        '        <div class="fw-bold text-primary fs-6">' + status + '</div>' +
        '        <div class="badge bg-primary-subtle text-primary border border-primary-subtle mt-1">' + stage + '</div>' +
        '      </div>' +
        '    </div>' +
        '    <div class="col-6 col-md-3">' +
        '      <div class="p-3 bg-white border rounded-3 shadow-xs h-100">' +
        '        <div class="extra-small text-uppercase text-muted fw-bold mb-1">Next Hearing Date</div>' +
        '        <div class="fw-bold text-success fs-5"><i class="bi bi-calendar-event me-1"></i>' + nextDate + '</div>' +
        '        <div class="extra-small text-muted mt-1">Filing: ' + filDate + '</div>' +
        '      </div>' +
        '    </div>' +
        '    <div class="col-6 col-md-3">' +
        '      <div class="p-3 bg-white border rounded-3 shadow-xs h-100">' +
        '        <div class="extra-small text-uppercase text-muted fw-bold mb-1">Court &amp; Judge</div>' +
        '        <div class="fw-semibold text-dark small" style="word-break: break-word;">' + courtJudge + '</div>' +
        '        <div class="extra-small text-muted mt-1">Reg: ' + regDate + '</div>' +
        '      </div>' +
        '    </div>' +
        '  </div>' +
        '</div>' +

        '<div class="px-4 pt-3">' +
        '  <ul class="nav nav-pills nav-fill bg-light p-1 rounded-3 border mb-3" id="modalDossierTab" role="tablist">' +
        '    <li class="nav-item"><button class="nav-link active fw-bold small py-2 rounded-2" data-bs-toggle="tab" data-bs-target="#mTabOverview" type="button"><i class="bi bi-card-text me-1 text-primary"></i> Case Overview &amp; Parties</button></li>' +
        '    <li class="nav-item"><button class="nav-link fw-bold small py-2 rounded-2" data-bs-toggle="tab" data-bs-target="#mTabHistory" type="button"><i class="bi bi-clock-history me-1 text-primary"></i> Roznama &amp; Hearings <span class="badge bg-primary ms-1">' + history.length + '</span></button></li>' +
        '    <li class="nav-item"><button class="nav-link fw-bold small py-2 rounded-2" data-bs-toggle="tab" data-bs-target="#mTabOrders" type="button"><i class="bi bi-file-earmark-pdf-fill me-1 text-danger"></i> Orders &amp; Judgments <span class="badge bg-danger ms-1">' + orders.length + '</span></button></li>' +
        '    <li class="nav-item"><button class="nav-link fw-bold small py-2 rounded-2" data-bs-toggle="tab" data-bs-target="#mTabIA" type="button"><i class="bi bi-journal-text me-1 text-warning"></i> Interim Applications <span class="badge bg-warning text-dark ms-1">' + ia_filings.length + '</span></button></li>' +
        (source === 'DC' ? '    <li class="nav-item"><button class="nav-link fw-bold small py-2 rounded-2" data-bs-toggle="tab" data-bs-target="#mTabProcess" type="button"><i class="bi bi-envelope-paper-fill me-1 text-info"></i> Notices &amp; Process <span class="badge bg-info text-dark ms-1">' + processes.length + '</span></button></li>' : '') +
        '    <li class="nav-item"><button class="nav-link fw-bold small py-2 rounded-2" data-bs-toggle="tab" data-bs-target="#mTabJson" type="button"><i class="bi bi-code-square me-1 text-dark"></i> Raw API JSON</button></li>' +
        '  </ul>' +

        '  <div class="tab-content pb-4">' +
        '    <!-- 1. Case Overview & Parties -->' +
        '    <div class="tab-pane fade show active" id="mTabOverview">' +
        '      <div class="row g-3 small">' +
        '        <div class="col-md-6">' +
        '          <div class="p-3 bg-light border-start border-4 border-primary rounded-3 shadow-xs h-100">' +
        '            <div class="extra-small text-uppercase text-muted fw-bold mb-1"><i class="bi bi-person-fill text-primary me-1"></i> Petitioner / Appellant</div>' +
        '            <div class="fw-bold text-dark fs-6 mb-2">' + pet + '</div>' +
        '            <div class="text-muted extra-small"><i class="bi bi-person-badge me-1"></i>Advocate: <strong class="text-dark">' + petAdv + '</strong></div>' +
        '          </div>' +
        '        </div>' +
        '        <div class="col-md-6">' +
        '          <div class="p-3 bg-light border-start border-4 border-secondary rounded-3 shadow-xs h-100">' +
        '            <div class="extra-small text-uppercase text-muted fw-bold mb-1"><i class="bi bi-building-fill text-secondary me-1"></i> Respondent / Defendant</div>' +
        '            <div class="fw-bold text-dark fs-6 mb-2">' + res + '</div>' +
        '            <div class="text-muted extra-small"><i class="bi bi-person-badge me-1"></i>Advocate: <strong class="text-dark">' + resAdv + '</strong></div>' +
        '          </div>' +
        '        </div>' +
        (extraParty && extraParty !== '—' ?
        '        <div class="col-12">' +
        '          <div class="p-3 bg-light border-start border-4 border-info rounded-3 shadow-xs">' +
        '            <div class="extra-small text-uppercase text-muted fw-bold mb-1"><i class="bi bi-people-fill text-info me-1"></i> Extra Parties / Insurance Company</div>' +
        '            <div class="fw-semibold text-dark">' + extraParty + '</div>' +
        '          </div>' +
        '        </div>' : '') +
        '      </div>' +
        '    </div>' +

        '    <!-- 2. Roznama & Hearings -->' +
        '    <div class="tab-pane fade" id="mTabHistory">' +
        '      <div class="table-responsive rounded-3 border" style="max-height: 420px; overflow-y: auto;">' +
        '        <table class="table table-sm table-hover align-middle mb-0">' +
        '          <thead class="bg-dark text-white sticky-top small text-uppercase">' +
        '            <tr><th style="width:40px;" class="text-center">#</th><th>Hearing Date</th><th>Purpose / Stage</th><th>Coram / Bench</th><th>Business Transacted / Roznama</th></tr>' +
        '          </thead>' +
        '          <tbody>' + histRows + '</tbody>' +
        '        </table>' +
        '      </div>' +
        '    </div>' +

        '    <!-- 3. Orders & Judgments -->' +
        '    <div class="tab-pane fade" id="mTabOrders">' +
        '      <div class="table-responsive rounded-3 border" style="max-height: 420px; overflow-y: auto;">' +
        '        <table class="table table-sm table-hover align-middle mb-0">' +
        '          <thead class="bg-dark text-white sticky-top small text-uppercase">' +
        '            <tr><th style="width:50px;" class="text-center">Order #</th><th>Date</th><th>Order Details</th><th>Bench</th><th class="text-end">Document PDF</th></tr>' +
        '          </thead>' +
        '          <tbody>' + orderRows + '</tbody>' +
        '        </table>' +
        '      </div>' +
        '    </div>' +

        '    <!-- 4. Interim Applications -->' +
        '    <div class="tab-pane fade" id="mTabIA">' +
        '      <div class="table-responsive rounded-3 border" style="max-height: 420px; overflow-y: auto;">' +
        '        <table class="table table-sm table-hover align-middle mb-0">' +
        '          <thead class="bg-dark text-white sticky-top small text-uppercase">' +
        '            <tr><th style="width:40px;" class="text-center">#</th><th>IA Number</th><th>Applicant</th><th>Relief / Prayer</th><th>Date of Filing</th></tr>' +
        '          </thead>' +
        '          <tbody>' + iaRows + '</tbody>' +
        '        </table>' +
        '      </div>' +
        '    </div>' +

        (source === 'DC' ?
        '    <!-- 5. Notices & Processes -->' +
        '    <div class="tab-pane fade" id="mTabProcess">' +
        '      <div class="table-responsive rounded-3 border" style="max-height: 420px; overflow-y: auto;">' +
        '        <table class="table table-sm table-hover align-middle mb-0">' +
        '          <thead class="bg-dark text-white sticky-top small text-uppercase">' +
        '            <tr><th style="width:40px;" class="text-center">#</th><th>Process ID</th><th>Issue Date</th><th>Notice Title</th><th>Served To / Party</th></tr>' +
        '          </thead>' +
        '          <tbody>' + procRows + '</tbody>' +
        '        </table>' +
        '      </div>' +
        '    </div>' : '') +

        '    <!-- 6. Raw API JSON -->' +
        '    <div class="tab-pane fade" id="mTabJson">' +
        '      <div class="bg-dark text-light p-3 rounded-3">' +
        '        <div class="d-flex justify-content-between align-items-center mb-2">' +
        '          <span class="extra-small text-light opacity-75 font-monospace">NAPIX eCourts JSON Gateway Payload</span>' +
        '          <button type="button" class="btn btn-xs btn-outline-light py-0 px-2" onclick="navigator.clipboard.writeText(document.getElementById(\'modalRawJsonContent\').innerText); alert(\'Raw JSON copied to clipboard!\');"><i class="bi bi-clipboard me-1"></i>Copy JSON</button>' +
        '        </div>' +
        '        <pre id="modalRawJsonContent" class="extra-small font-monospace mb-0 text-warning" style="max-height: 400px; overflow-y: auto; white-space: pre-wrap;">' + rawJsonStr + '</pre>' +
        '      </div>' +
        '    </div>' +
        '  </div>' +
        '</div>';

    bodyElem.innerHTML = html;
}

/* ────────────────────────────────────────────────────
   Interactive Modal 2: Hearing Details & Live Daily Status (NAPIX)
──────────────────────────────────────────────────── */
function printDailyStatusSheet() {
    var printContent = document.getElementById('ecourtsDailyStatusPrintArea');
    if (!printContent) return;

    var printWin = window.open('', '_blank', 'width=860,height=900');
    if (!printWin) {
        window.print();
        return;
    }

    var doc = printWin.document;
    doc.open();
    doc.write('<!DOCTYPE html><html><head><title>eCourts Daily Status - Court Proceedings</title>');
    doc.write('<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css">');
    doc.write('<style>');
    doc.write('@page { size: A4 portrait; margin: 12mm 15mm; }');
    doc.write('body { font-family: "Georgia", "Times New Roman", serif; color: #111; background: #fff; padding: 15px; }');
    doc.write('.table-bordered th, .table-bordered td { border: 1px solid #222 !important; padding: 6px 10px; font-size: 13px; }');
    doc.write('.border-dark { border-color: #222 !important; }');
    doc.write('#roznamaText { font-family: "Georgia", "Times New Roman", serif; font-size: 14px; line-height: 1.7; white-space: pre-wrap; }');
    doc.write('@media print { .no-print { display: none !important; } body { padding: 0; } }');
    doc.write('</style></head><body>');
    doc.write(printContent.innerHTML);
    doc.write('</body></html>');
    doc.close();
    printWin.focus();
    setTimeout(function() {
        printWin.print();
        printWin.close();
    }, 450);
}
window.printDailyStatusSheet = printDailyStatusSheet;

function openHearingDetailsModal(source, index, appealId, explicitDate) {
    var modalElem = document.getElementById('ecourtsHearingModal');
    if (!modalElem) {
        var modalHtml =
            '<div class="modal fade" id="ecourtsHearingModal" tabindex="-1" aria-hidden="true">' +
            '  <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">' +
            '    <div class="modal-content border-0 shadow-lg">' +
            '      <div class="modal-header bg-dark text-white py-2 px-3 d-flex justify-content-between align-items-center">' +
            '        <div class="d-flex align-items-center gap-2">' +
            '          <i class="bi bi-journal-bookmark-fill text-warning fs-5"></i>' +
            '          <div>' +
            '            <h6 class="modal-title fw-bold mb-0 text-uppercase" style="letter-spacing: 0.5px;">e-Courts Services &bull; Daily Status</h6>' +
            '            <span class="text-light extra-small opacity-75">National Judicial Data Grid (NJDG) Roznama</span>' +
            '          </div>' +
            '        </div>' +
            '        <div class="d-flex align-items-center gap-2">' +
            '          <button type="button" class="btn btn-sm btn-light fw-bold text-dark px-3 shadow-sm rounded-pill" onclick="printDailyStatusSheet()" title="Print or Save as PDF">' +
            '            <i class="bi bi-printer-fill text-primary me-1"></i> Print Daily Status' +
            '          </button>' +
            '          <button type="button" class="btn btn-sm btn-outline-light rounded-pill px-3" id="btnCopyRoznama" title="Copy verbatim proceedings to clipboard">' +
            '            <i class="bi bi-clipboard me-1"></i> Copy Roznama' +
            '          </button>' +
            '          <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>' +
            '        </div>' +
            '      </div>' +
            '      <div class="modal-body p-3 bg-light" id="ecourtsHearingModalBody">' +
            '      </div>' +
            '      <div class="modal-footer bg-white py-2 px-3 justify-content-between border-top">' +
            '        <span class="text-muted extra-small"><i class="bi bi-shield-check text-success me-1"></i> Authentic e-Courts Daily Status (Roznama)</span>' +
            '        <button type="button" class="btn btn-sm btn-secondary rounded-pill px-4" data-bs-dismiss="modal">Close</button>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';
        document.body.insertAdjacentHTML('beforeend', modalHtml);
        modalElem = document.getElementById('ecourtsHearingModal');
    }

    var bodyElem = document.getElementById('ecourtsHearingModalBody');
    var copyBtn = document.getElementById('btnCopyRoznama');

    var item = null;
    var cnr = null;
    var caseNum = '—';
    var pet = '—';
    var petAdv = '—';
    var res = '—';
    var resAdv = '—';
    var courtName = 'DISTRICT & SESSIONS COURT / MACT';
    var topJudge = 'Presiding Officer';

    if (source === 'HC') {
        if (appealId && window.lastMfaData && window.lastMfaData[appealId]) {
            var mfa = window.lastMfaData[appealId];
            if (index >= 0) {
                item = (mfa.history || [])[index];
            } else if (explicitDate && mfa.history && mfa.history.length > 0) {
                for (var hi = 0; hi < mfa.history.length; hi++) {
                    var hItem = mfa.history[hi];
                    var itemDt = extractValidDate(getObjProp(hItem, 'business_date', 'hearing_date', 'date'));
                    if (itemDt === explicitDate) {
                        item = hItem;
                        break;
                    }
                }
            }
            cnr = mfa.cnr;
            caseNum = mfa.case_number || 'High Court Writ Petition / Appeal';
            pet = mfa.petitioner || '—';
            res = mfa.respondent || '—';
            topJudge = mfa.court_judge || 'HIGH COURT BENCH';
            courtName = 'HIGH COURT OF KARNATAKA';
            var dObj = mfa.raw || mfa.data || {};
            petAdv = getObjProp(dObj, 'petitioner_advocate', 'pet_adv', 'petadv') || '—';
            resAdv = getObjProp(dObj, 'respondent_advocate', 'res_adv', 'resadv') || '—';
        }

        if (!item && explicitDate) {
            item = {
                business_date: explicitDate,
                hearing_date: explicitDate,
                purpose_of_listing: 'Scheduled High Court Hearing',
                judge: topJudge || 'High Court Bench',
                business: 'Listing scheduled for ' + explicitDate + '. Live Daily Status will query e-Courts showBusiness gateway for this date.'
            };
        }

        if (!cnr && appealId) {
            var appealInput = document.getElementById('cnrInput_' + appealId);
            if (appealInput && appealInput.value.trim()) {
                cnr = appealInput.value.trim().toUpperCase();
            }
        }
        if (!cnr) {
            var anyHcInput = document.querySelector('[id^="cnrInput_"]');
            if (anyHcInput && anyHcInput.value.trim().length >= 12) {
                cnr = anyHcInput.value.trim().toUpperCase();
            }
        }
    } else if (window.lastDcData) {
        var dc = window.lastDcData;
        item = (dc.history || [])[index];
        cnr = dc.cnr;
        var dObj = dc.data || dc.raw || {};
        caseNum = getObjProp(dObj, 'case_no', 'reg_no', 'cino', 'case_number', 'filing_no') || '—';
        pet = getObjProp(dObj, 'petitioner', 'pet_name') || dc.petitioner || '—';
        petAdv = getObjProp(dObj, 'petitioner_advocate', 'pet_adv', 'pet_advocate') || '—';
        res = getObjProp(dObj, 'respondent', 'res_name') || dc.respondent || '—';
        resAdv = getObjProp(dObj, 'respondent_advocate', 'res_adv', 'res_advocate') || '—';
        courtName = getObjProp(dObj, 'court_name', 'dist_name', 'court_complex_name') || 'DISTRICT & SESSIONS COURT / MACT';
        topJudge = getObjProp(dObj, 'court_no_judge', 'judge') || dc.court_judge || 'Presiding Officer';
    }

    if (!cnr && source !== 'HC') {
        cnr = window.lastCnr || '';
    }

    if (!item) {
        bodyElem.innerHTML = '<div class="alert alert-warning">Hearing record details not found.</div>';
        return;
    }

    var bDate = getObjProp(item, 'business_date', 'date', 'cause_date', 'dt_business', 'srno_date') || '—';
    var hDate = getObjProp(item, 'hearing_date', 'next_date', 'next_hearing_date') || '—';
    var pur = getObjProp(item, 'purpose_of_listing', 'purpose_name', 'stage', 'purpose', 'purpose_of_hearing') || 'Hearing';
    var jdg = getObjProp(item, 'judge', 'judge_name', 'court_no_judge', 'presiding_officer', 'desgname') || topJudge || '—';
    // Extract roznama/business text using EXACT key matching ONLY
    // (getObjProp does fuzzy/substring match, which returns business_date for 'business' key lookup — wrong!)
    // Bad values that APIs store when no data is available
    var BAD_ROZNAMA_VALUES = /^(null|undefined|n\/a|na|none|—|-|--|no\s*data|not\s*available|not\s*given)$/i;
    function getExactProp(obj, keyList) {
        if (!obj || typeof obj !== 'object') return null;
        var lowerKeys = Object.keys(obj).map(function(k) { return k.toLowerCase(); });
        for (var ii = 0; ii < keyList.length; ii++) {
            var kw = keyList[ii].toLowerCase();
            var idx = lowerKeys.indexOf(kw);
            if (idx >= 0) {
                var v = obj[Object.keys(obj)[idx]];
                if (v && typeof v === 'string') {
                    var t = v.trim();
                    // Reject: too short, known placeholder strings, or looks like a pure date
                    if (t.length < 10) continue;
                    if (BAD_ROZNAMA_VALUES.test(t)) continue;
                    if (/^\d{2,4}[\-\/]\d{1,2}[\-\/]\d{1,4}$/.test(t)) continue; // date-only string
                    return t;
                }
            }
        }
        return null;
    }
    var roznamaExact = getExactProp(item, ['business', 'roznama', 'proceedings', 'court_business', 'business_details', 'order_business', 'order_details', 'short_order', 'short_order_det', 'order_text', 'court_order', 'daily_order']);
    var roznama = (roznamaExact && roznamaExact.trim().length > 5)
        ? roznamaExact.trim()
        : null; // will show structured placeholder below

    var queryDate = extractValidDate(explicitDate) ||
                    extractValidDate(bDate) ||
                    extractValidDate(hDate) ||
                    (bDate && bDate !== '—' && !bDate.toLowerCase().includes('not given') ? bDate : hDate);

    var html =
        '<div id="liveDailyStatusSection" class="alert alert-info py-2 px-3 mb-3 d-flex align-items-center justify-content-between">' +
        '  <div class="d-flex align-items-center gap-2">' +
        '    <i class="bi bi-broadcast text-primary fs-5"></i>' +
        '    <div>' +
        '      <div class="d-flex align-items-center gap-2">' +
        '        <strong class="text-primary small">e-Courts NAPIX Live Daily Status Gateway</strong>' +
        '        <span id="liveDailyStatusBadge" class="badge bg-warning text-dark extra-small"><span class="spinner-border spinner-border-sm me-1" style="width:0.65rem;height:0.65rem;"></span> Checking Live...</span>' +
        '      </div>' +
        '      <span id="liveDailyStatusMsg" class="text-muted extra-small">Connecting to e-Courts Daily Status (showBusiness) gateway for CNR ' + (cnr || '—') + ' on ' + queryDate + '...</span>' +
        '    </div>' +
        '  </div>' +
        '  <button type="button" class="btn btn-sm btn-outline-primary rounded-pill px-3 shadow-sm" id="btnRefreshDailyStatus">' +
        '    <i class="bi bi-arrow-clockwise me-1"></i> Re-fetch Live' +
        '  </button>' +
        '</div>' +

        '<!-- Authentic eCourts Daily Status Printable Sheet -->' +
        '<div id="ecourtsDailyStatusPrintArea" class="border border-dark p-4 bg-white shadow-sm mb-3" style="border-width: 2px !important;">' +
        '  <div class="text-center pb-3 mb-3 border-bottom border-dark">' +
        '    <div class="d-flex align-items-center justify-content-center gap-2 mb-1">' +
        '      <span class="badge px-3 py-1 text-uppercase fw-bold text-white" style="background-color: #1e3a8a; letter-spacing: 1px; font-size: 11px;">e-Courts Services &bull; NJDG</span>' +
        '    </div>' +
        '    <h5 class="fw-bold text-uppercase mb-1 text-dark" style="font-family: \'Georgia\', serif; letter-spacing: 0.5px;">' + (source === 'HC' ? 'HIGH COURT OF KARNATAKA' : courtName) + '</h5>' +
        '    <div class="fw-semibold text-secondary small mb-2" id="dsCourtHeading">' + (jdg && jdg !== '—' ? 'IN THE COURT OF: ' + jdg : '') + '</div>' +
        '    <div class="d-inline-block px-4 py-1 border border-dark rounded-pill fw-bold text-uppercase text-dark" style="background: #f8fafc; font-size: 12px; letter-spacing: 1px;">DAILY STATUS / PROCEEDINGS (ROZNAMA)</div>' +
        '  </div>' +

        '  <!-- 2-Column Official Case Information Table -->' +
        '  <table class="table table-bordered table-sm mb-3 align-middle" style="border-color: #333; font-size: 13px;">' +
        '    <tbody>' +
        '      <tr>' +
        '        <th style="width: 20%; background-color: #f1f5f9;" class="text-secondary">Case No / Type</th>' +
        '        <td style="width: 30%;" class="fw-bold text-dark font-monospace" id="dsCaseNum">' + caseNum + '</td>' +
        '        <th style="width: 20%; background-color: #f1f5f9;" class="text-secondary">CNR Number</th>' +
        '        <td style="width: 30%;" class="fw-bold text-primary font-monospace" id="dsCnr">' + (cnr || '—') + '</td>' +
        '      </tr>' +
        '      <tr>' +
        '        <th style="background-color: #f1f5f9;" class="text-secondary">Petitioner / Appellant</th>' +
        '        <td class="fw-semibold text-dark">' + pet + '</td>' +
        '        <th style="background-color: #f1f5f9;" class="text-secondary">Petitioner Advocate</th>' +
        '        <td class="text-dark">' + petAdv + '</td>' +
        '      </tr>' +
        '      <tr>' +
        '        <th style="background-color: #f1f5f9;" class="text-secondary">Respondent</th>' +
        '        <td class="fw-semibold text-dark">' + res + '</td>' +
        '        <th style="background-color: #f1f5f9;" class="text-secondary">Respondent Advocate</th>' +
        '        <td class="text-dark">' + resAdv + '</td>' +
        '      </tr>' +
        '      <tr>' +
        '        <th style="background-color: #f1f5f9;" class="text-secondary">Court / Judge (Coram)</th>' +
        '        <td colspan="3" class="fw-bold text-dark" id="dsCoram">' + jdg + '</td>' +
        '      </tr>' +
        '      <tr style="background-color: #fcfcfc;">' +
        '        <th style="background-color: #e2e8f0;" class="text-dark fw-bold">Business on Date</th>' +
        '        <td class="fw-bold text-dark" id="dsBusinessDate"><i class="bi bi-calendar-check me-1 text-primary"></i>' + bDate + '</td>' +
        '        <th style="background-color: #e2e8f0;" class="text-dark fw-bold">Next Hearing Date</th>' +
        '        <td class="fw-bold text-success fs-6" id="dsNextHearingDate"><i class="bi bi-calendar-event me-1 text-success"></i>' + hDate + '</td>' +
        '      </tr>' +
        '      <tr>' +
        '        <th style="background-color: #f1f5f9;" class="text-secondary">Purpose of Hearing</th>' +
        '        <td colspan="3" class="fw-bold text-primary" id="dsPurpose">' + pur + '</td>' +
        '      </tr>' +
        '    </tbody>' +
        '  </table>' +

        '  <!-- Verbatim Proceedings / Roznama Box -->' +
        '  <div class="mt-3">' +
        '    <div class="d-flex justify-content-between align-items-center py-2 px-3 border border-bottom-0 border-dark bg-light">' +
        '      <strong class="text-uppercase small text-dark" style="letter-spacing: 0.5px;"><i class="bi bi-file-earmark-text-fill text-primary me-1"></i> Business Transacted (Daily Order / Roznama)</strong>' +
        '      <span id="roznamaSourceBadge" class="badge bg-secondary extra-small">Recorded in Case History</span>' +
        '    </div>' +
        '    <div id="roznamaText" class="p-3 border border-dark text-dark" style="white-space: pre-wrap; min-height: 140px; max-height: 320px; overflow-y: auto; font-family: \'Georgia\', \'Times New Roman\', serif; font-size: 14px; line-height: 1.7; background: #fff;">' + (roznama || ('📋 Hearing Date: ' + (bDate !== '—' ? bDate : hDate) + '\nPurpose / Stage: ' + pur + '\nPresiding Officer: ' + jdg + '\n\n⏳ Querying e-Courts NAPIX gateway for verbatim proceedings on this date...\nIf no data is returned, the daily order for this hearing may not be digitally available on NJDG for this date.')) + '</div>' +
        '  </div>' +

        '  <div class="text-center extra-small text-muted pt-3 mt-3 border-top border-dark d-flex justify-content-between align-items-center">' +
        '    <span>National Judicial Data Grid (NJDG)</span>' +
        '    <span>Official e-Courts Daily Status Record</span>' +
        '    <span>Printed / Generated: ' + new Date().toLocaleDateString('en-GB') + '</span>' +
        '  </div>' +
        '</div>';

    bodyElem.innerHTML = html;

    if (copyBtn) {
        copyBtn.onclick = function() {
            var txt = document.getElementById('roznamaText').innerText;
            navigator.clipboard.writeText(txt);
            alert('Roznama proceedings copied to clipboard!');
        };
    }

    var refreshBtn = document.getElementById('btnRefreshDailyStatus');
    if (refreshBtn) {
        refreshBtn.onclick = function() {
            fetchLiveDailyStatus(source, cnr, queryDate);
        };
    }

    if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
        var bs = bootstrap.Modal.getInstance(modalElem) || new bootstrap.Modal(modalElem);
        bs.show();
    } else {
        $(modalElem).modal('show');
    }

    // Trigger live NAPIX fetch immediately
    fetchLiveDailyStatus(source, cnr, queryDate);
}

function fetchLiveDailyStatus(source, cnr, queryDate) {
    var badge = document.getElementById('liveDailyStatusBadge');
    var msg = document.getElementById('liveDailyStatusMsg');
    var roznamaText = document.getElementById('roznamaText');
    var srcBadge = document.getElementById('roznamaSourceBadge');
    var rawPre = document.getElementById('rawDailyStatusJson');
    var nextHearingDateEl = document.getElementById('dsNextHearingDate');
    var purposeEl = document.getElementById('dsPurpose');
    var coramEl = document.getElementById('dsCoram');
    var courtHeadingEl = document.getElementById('dsCourtHeading');
    var caseNumEl = document.getElementById('dsCaseNum');
    var cnrEl = document.getElementById('dsCnr');
    var businessDateEl = document.getElementById('dsBusinessDate');

    if (!cnr || !queryDate || queryDate === '—') {
        if (badge) {
            badge.className = 'badge bg-secondary-subtle text-secondary border px-2 py-1 extra-small';
            badge.innerHTML = '<i class="bi bi-info-circle me-1"></i> Recorded in Dossier';
        }
        if (msg) msg.innerText = 'Hearing or business date not specified to query live e-Courts showBusiness gateway.';
        if (rawPre) rawPre.innerText = 'No query executed: missing CNR or date.';
        return;
    }

    if (badge) {
        badge.className = 'badge bg-warning text-dark px-2 py-1 extra-small';
        badge.innerHTML = '<span class="spinner-border spinner-border-sm me-1" style="width: 0.7rem; height: 0.7rem;"></span> Querying NAPIX...';
    }
    if (msg) msg.innerText = 'Querying live showBusiness gateway for CNR ' + cnr + ' on ' + queryDate + '...';

    var isHc = (source === 'HC');
    var url = '/ECourts/GetCaseBusiness?cnrNumber=' + encodeURIComponent(cnr) + '&date=' + encodeURIComponent(queryDate) + '&isHighCourt=' + isHc;

    fetch(url)
        .then(function(r) { return r.json(); })
        .then(function(res) {
            if (rawPre) rawPre.innerText = JSON.stringify(res, null, 2);

            if (res && res.success && res.data) {
                var d = res.data;
                if (badge) {
                    badge.className = 'badge bg-success px-2 py-1 extra-small';
                    badge.innerHTML = '<i class="bi bi-check-circle-fill me-1"></i> Live NAPIX Synced';
                }
                if (msg) msg.innerText = 'Live Daily Status successfully retrieved from e-Courts NAPIX Gateway.';

                // Extract verbatim roznama / business — deep scan all nested objects/arrays
                function deepFindBusiness(obj, depth, fromKey) {
                    if (!obj || depth > 8) return null;
                    var keys = ['business', 'roznama', 'business_details', 'proceedings', 'court_business', 'order_business', 'order_details', 'short_order', 'short_order_det', 'order_text'];
                    // Only return a string if we got here from a known key or it's a long meaningful text
                    if (typeof obj === 'string') {
                        var t = obj.trim();
                        if (fromKey && t.length > 5) return t; // from a known key, any length OK
                        if (t.length >= 30) return t; // long text likely is roznama content
                        return null;
                    }
                    if (Array.isArray(obj)) {
                        for (var i2 = 0; i2 < obj.length; i2++) {
                            var r2 = deepFindBusiness(obj[i2], depth + 1, fromKey);
                            if (r2 && r2.trim().length > 5) return r2;
                        }
                        return null;
                    }
                    if (typeof obj === 'object') {
                        var objKeys = Object.keys(obj);
                        // First pass: check direct known keys
                        for (var ki = 0; ki < keys.length; ki++) {
                            var k = keys[ki];
                            var found = null;
                            if (obj[k] !== undefined && obj[k] !== null) found = obj[k];
                            if (!found) {
                                for (var j = 0; j < objKeys.length; j++) {
                                    if (objKeys[j].toLowerCase() === k) { found = obj[objKeys[j]]; break; }
                                }
                            }
                            if (found !== null && found !== undefined) {
                                var result = deepFindBusiness(found, depth + 1, true);
                                if (result && result.trim().length > 5) return result;
                            }
                        }
                        // Second pass: recurse into ALL sub-objects (but not as fromKey)
                        for (var a = 0; a < objKeys.length; a++) {
                            if (typeof obj[objKeys[a]] === 'object') {
                                var sub = deepFindBusiness(obj[objKeys[a]], depth + 1, false);
                                if (sub && sub.trim().length > 5) return sub;
                            }
                        }
                    }
                    return null;
                }
                var liveRoznama = deepFindBusiness(d, 0, false) ||
                                  (res.raw ? deepFindBusiness(res.raw, 0, false) : null);

                if (liveRoznama && liveRoznama.trim().length > 0) {
                    var cleanRoznama = decodeHtmlEntities(liveRoznama.trim());
                    if (roznamaText) roznamaText.innerText = cleanRoznama;
                    if (srcBadge) {
                        srcBadge.className = 'badge bg-success extra-small';
                        srcBadge.innerHTML = '<i class="bi bi-broadcast me-1"></i> Live NAPIX Roznama';
                    }
                }

                // Update official eCourts table fields with live data
                var liveJudge = getObjProp(d, 'judge_name', 'judge', 'court_no_judge', 'presiding_officer', 'coram', 'desgname');
                var liveNextDt = getObjProp(d, 'next_date', 'next_hearing_date', 'dt_next_hearing', 'next_dt');
                var livePurpose = getObjProp(d, 'purpose_of_listing', 'purpose_name', 'stage', 'purpose', 'short_order_det', 'short_order');
                var liveCaseNo = getObjProp(d, 'case_number', 'case_no', 'reg_no');
                var liveBDate = getObjProp(d, 'business_date', 'date');

                if (liveNextDt && liveNextDt !== '—' && nextHearingDateEl) {
                    nextHearingDateEl.innerHTML = '<i class="bi bi-calendar-event me-1 text-success"></i>' + liveNextDt;
                }
                if (livePurpose && livePurpose !== '—' && purposeEl) {
                    purposeEl.innerText = livePurpose;
                }
                if (liveJudge && liveJudge !== '—') {
                    if (coramEl) coramEl.innerText = liveJudge;
                    if (courtHeadingEl) courtHeadingEl.innerText = 'IN THE COURT OF: ' + liveJudge;
                }
                if (liveCaseNo && caseNumEl && (caseNumEl.innerText === '—' || caseNumEl.innerText.includes('Appeal'))) {
                    caseNumEl.innerText = liveCaseNo;
                }
                if (liveBDate && businessDateEl) {
                    businessDateEl.innerHTML = '<i class="bi bi-calendar-check me-1 text-primary"></i>' + liveBDate;
                }
            } else {
                if (badge) {
                    badge.className = 'badge bg-secondary-subtle text-secondary border px-2 py-1 extra-small';
                    badge.innerHTML = '<i class="bi bi-info-circle me-1"></i> Not Available on NAPIX';
                }
                var napixMsg = (res && res.message) ? res.message : 'No Daily Status record found on NAPIX gateway for this date.';
                if (msg) msg.innerText = napixMsg + ' For older hearings, verbatim daily orders may not be digitally published on NJDG.';
                // Update the roznama box to show a clear "not available" message if still showing spinner
                var rzBox = document.getElementById('roznamaText');
                if (rzBox) {
                    var currentText = rzBox.innerText || '';
                    if (currentText.indexOf('\u23f3') >= 0 || currentText.indexOf('Querying') >= 0) {
                        rzBox.innerText = '\u26a0\ufe0f Daily proceedings (Roznama) not available on NJDG for this hearing date.\n\n' +
                            'This may be because:\n' +
                            '  \u2022 The hearing is from before NJDG digital records were maintained\n' +
                            '  \u2022 The daily order was not uploaded to e-Courts for this date\n' +
                            '  \u2022 The CNR may need to be updated for this case\n\n' +
                            'Hearing Date: ' + (document.getElementById('dsBusinessDate') ? document.getElementById('dsBusinessDate').innerText.replace(/[^0-9\-\/]/g, '') : '—') + '\n' +
                            'Purpose: ' + (document.getElementById('dsPurpose') ? document.getElementById('dsPurpose').innerText : '—');
                    }
                }
            }
        })
        .catch(function(err) {
            console.warn('Live daily status fetch error:', err);
            if (badge) {
                badge.className = 'badge bg-warning-subtle text-dark border px-2 py-1 extra-small';
                badge.innerHTML = '<i class="bi bi-exclamation-circle me-1"></i> Recorded in Case History';
            }
            if (msg) msg.innerText = 'Unable to reach live NAPIX showBusiness gateway. Showing recorded proceedings.';
            if (rawPre) rawPre.innerText = 'Network error fetching live NAPIX data: ' + err;
        });
}

/* ────────────────────────────────────────────────────
   Interactive Modal 3: Process & Notice Details Modal
──────────────────────────────────────────────────── */
function openProcessDetailsModal(source, index) {
    var modalElem = document.getElementById('ecourtsProcessModal');
    if (!modalElem) {
        var modalHtml =
            '<div class="modal fade" id="ecourtsProcessModal" tabindex="-1" aria-hidden="true">' +
            '  <div class="modal-dialog modal-dialog-centered">' +
            '    <div class="modal-content border-0 shadow-lg">' +
            '      <div class="modal-header bg-dark text-white py-3 px-4">' +
            '        <div class="d-flex align-items-center gap-2">' +
            '          <i class="bi bi-envelope-paper-fill text-info fs-4"></i>' +
            '          <div>' +
            '            <h5 class="modal-title fw-bold mb-0" id="ecourtsProcessModalTitle">Process &amp; Notice Service</h5>' +
            '            <span class="text-light extra-small opacity-75">Court Summons / Process Details</span>' +
            '          </div>' +
            '        </div>' +
            '        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>' +
            '      </div>' +
            '      <div class="modal-body p-4" id="ecourtsProcessModalBody">' +
            '      </div>' +
            '      <div class="modal-footer bg-light py-2 px-4">' +
            '        <button type="button" class="btn btn-sm btn-secondary rounded-pill px-4" data-bs-dismiss="modal">Close</button>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';
        document.body.insertAdjacentHTML('beforeend', modalHtml);
        modalElem = document.getElementById('ecourtsProcessModal');
    }

    var bodyElem = document.getElementById('ecourtsProcessModalBody');
    var item = (window.lastDcData && window.lastDcData.processes) ? window.lastDcData.processes[index] : null;

    if (!item) {
        bodyElem.innerHTML = '<div class="alert alert-warning">Process record not found.</div>';
        return;
    }

    var pId = getObjProp(item, 'process_id', 'id', 'process_no') || ('P-' + (index + 1));
    var pDt = getObjProp(item, 'process_date', 'date', 'issue_date') || '—';
    var pTitle = getObjProp(item, 'process_title', 'title', 'process_name') || 'Summons / Notice';
    var party = getObjProp(item, 'party_name', 'served_to', 'recipient') || '—';
    var returnDt = getObjProp(item, 'return_date', 'next_date', 'compliance_date', 'status') || '—';

    var html =
        '<div class="p-3 bg-light rounded border mb-3">' +
        '  <div class="extra-small text-uppercase text-muted fw-bold mb-1">Process Identification</div>' +
        '  <div class="fs-5 fw-bold text-primary font-monospace">' + pId + '</div>' +
        '  <div class="fw-semibold text-dark mt-1">' + pTitle + '</div>' +
        '</div>' +
        '<div class="row g-3 small">' +
        '  <div class="col-6">' +
        '    <div class="p-2 bg-light border rounded"><span class="text-muted d-block extra-small">Issue Date</span><strong class="text-dark">' + pDt + '</strong></div>' +
        '  </div>' +
        '  <div class="col-6">' +
        '    <div class="p-2 bg-light border rounded"><span class="text-muted d-block extra-small">Return Date / Status</span><strong class="text-primary">' + returnDt + '</strong></div>' +
        '  </div>' +
        '  <div class="col-12">' +
        '    <div class="p-2 bg-light border rounded"><span class="text-muted d-block extra-small">Served To / Recipient</span><strong class="text-dark">' + party + '</strong></div>' +
        '  </div>' +
        '</div>';

    bodyElem.innerHTML = html;

    if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
        var bs = bootstrap.Modal.getInstance(modalElem) || new bootstrap.Modal(modalElem);
        bs.show();
    } else {
        $(modalElem).modal('show');
    }
}
