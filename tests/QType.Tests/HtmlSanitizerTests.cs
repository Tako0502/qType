// Wired up later when QType.Web is added as a reference. For now, the sanitizer
// is exercised via the live API. Adversarial inputs to cover when this lands as a unit test:
//   - "<script>alert('xss')</script>"          → stripped, no <script>
//   - "<p onclick='evil()'>text</p>"           → <p>text</p>  (attribute dropped)
//   - "<img src=x onerror=alert(1)>"           → "" (tag not in allow list)
//   - "<iframe src=//evil.com></iframe>"       → stripped, entire block removed
//   - "<p><b>safe</b></p>"                     → preserved as-is
//   - "<P STYLE='display:none'>text</P>"       → <p>text</p>
