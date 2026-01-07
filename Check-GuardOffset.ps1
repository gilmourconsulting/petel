Write-Host "🛡️ Guard Off-Set Functionality Diagnostic" -ForegroundColor Cyan
Write-Host ""
Write-Host "This feature sets Guard cost to 0 when:" -ForegroundColor Yellow
Write-Host "  1. School has 'Guard off-set' attribute = true" -ForegroundColor White
Write-Host "  2. Student's sending council = School's council" -ForegroundColor White
Write-Host ""

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""

Write-Host "📋 Checklist to verify Guard off-set is working:" -ForegroundColor Cyan
Write-Host ""

Write-Host "STEP 1: Verify the code is deployed" -ForegroundColor Yellow
Write-Host "  ✓ Check DLL timestamp:" -ForegroundColor White
Write-Host "    - Open: https://petel-test-api.scm.azurewebsites.net" -ForegroundColor Gray
Write-Host "    - Navigate: site/wwwroot" -ForegroundColor Gray
Write-Host "    - Check: PetelApp.Api.dll modified date" -ForegroundColor Gray
Write-Host "    - Should be: Today's date" -ForegroundColor Gray
Write-Host ""

Write-Host "STEP 2: Verify database setup" -ForegroundColor Yellow
Write-Host "  Run these SQL queries in your database:" -ForegroundColor White
Write-Host ""
Write-Host "  -- Check if 'Guard off-set' attribute type exists" -ForegroundColor Gray
Write-Host "  SELECT * FROM petel_schema.school_attribute_types" -ForegroundColor Cyan
Write-Host "  WHERE name = 'Guard off-set';" -ForegroundColor Cyan
Write-Host "  -- Should return 1 row" -ForegroundColor Gray
Write-Host ""
Write-Host "  -- Check if a school has this attribute enabled" -ForegroundColor Gray
Write-Host "  SELECT s.school_name, sa.value, sat.name" -ForegroundColor Cyan
Write-Host "  FROM petel_schema.school_attributes sa" -ForegroundColor Cyan
Write-Host "  JOIN petel_schema.schools s ON sa.school_year_id = s.school_year_id" -ForegroundColor Cyan
Write-Host "  JOIN petel_schema.school_attribute_types sat ON sa.school_attribute_type_id = sat.id" -ForegroundColor Cyan
Write-Host "  WHERE sat.name = 'Guard off-set'" -ForegroundColor Cyan
Write-Host "  AND sa.is_last_version = true;" -ForegroundColor Cyan
Write-Host "  -- Should show schools with Guard off-set and their value (true/false/1/0)" -ForegroundColor Gray
Write-Host ""

Write-Host "STEP 3: Verify student/school council match" -ForegroundColor Yellow
Write-Host "  Run this query for a specific student:" -ForegroundColor White
Write-Host ""
Write-Host "  -- Replace 204 with your student ID" -ForegroundColor Gray
Write-Host "  SELECT " -ForegroundColor Cyan
Write-Host "    ss.id as student_id," -ForegroundColor Cyan
Write-Host "    ss.id_number," -ForegroundColor Cyan
Write-Host "    ss.name," -ForegroundColor Cyan
Write-Host "    ss.sending_council as student_council," -ForegroundColor Cyan
Write-Host "    s.council as school_council," -ForegroundColor Cyan
Write-Host "    CASE WHEN ss.sending_council = s.council THEN 'MATCH' ELSE 'NO MATCH' END as match_status," -ForegroundColor Cyan
Write-Host "    sa.value as guard_offset_enabled" -ForegroundColor Cyan
Write-Host "  FROM petel_schema.school_students ss" -ForegroundColor Cyan
Write-Host "  JOIN petel_schema.schools s ON ss.school_year_id = s.school_year_id AND s.is_last_version = true" -ForegroundColor Cyan
Write-Host "  LEFT JOIN petel_schema.school_attributes sa ON sa.school_year_id = ss.school_year_id AND sa.is_last_version = true" -ForegroundColor Cyan
Write-Host "  LEFT JOIN petel_schema.school_attribute_types sat ON sa.school_attribute_type_id = sat.id AND sat.name = 'Guard off-set'" -ForegroundColor Cyan
Write-Host "  WHERE ss.id = 204;" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Expected results for Guard off-set to work:" -ForegroundColor Gray
Write-Host "    - match_status = 'MATCH'" -ForegroundColor Green
Write-Host "    - guard_offset_enabled = 'true' or '1'" -ForegroundColor Green
Write-Host ""

Write-Host "STEP 4: Check application logs" -ForegroundColor Yellow
Write-Host "  The code logs messages when processing Guard off-set:" -ForegroundColor White
Write-Host ""
Write-Host "  - Open: https://petel-test-api.scm.azurewebsites.net/api/logs/docker" -ForegroundColor Gray
Write-Host "  - Calculate pricing for a student" -ForegroundColor Gray
Write-Host "  - Look for these log messages:" -ForegroundColor Gray
Write-Host "    * '🛡️ Guard off-set is enabled. Checking council match...'" -ForegroundColor Green
Write-Host "    * '✅ Guard off-set applied: Student council X matches school council Y'" -ForegroundColor Green
Write-Host "    * '⚠️ Guard off-set NOT applied: Student council X does not match...'" -ForegroundColor Yellow
Write-Host ""

Write-Host "STEP 5: Test the functionality" -ForegroundColor Yellow
Write-Host "  1. Login to: https://petel-test-api.azurewebsites.net" -ForegroundColor White
Write-Host "  2. Navigate to a student with matching councils" -ForegroundColor White
Write-Host "  3. Click 'חשב תמחור' (Calculate Pricing)" -ForegroundColor White
Write-Host "  4. Check the pricing modal for 'Guard' element" -ForegroundColor White
Write-Host "  5. If councils match and attribute is true:" -ForegroundColor White
Write-Host "     - Price should be: ₪0.00" -ForegroundColor Green
Write-Host "     - 'גורם לחישוב' should show: 'תלמיד מרשות בית הספר'" -ForegroundColor Green
Write-Host ""

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""
Write-Host "🔧 If still not working after verification:" -ForegroundColor Red
Write-Host ""
Write-Host "  Option 1: Force redeploy" -ForegroundColor Yellow
Write-Host "    .\Force-Redeploy.ps1 -Environment test" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Option 2: Check if 'Guard' pricing element exists" -ForegroundColor Yellow
Write-Host "    SELECT * FROM petel_schema.special_needs_pricing_elements" -ForegroundColor Cyan
Write-Host "    WHERE element_name ILIKE '%guard%' OR title ILIKE '%guard%';" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Option 3: Verify student has Guard in their pricing" -ForegroundColor Yellow
Write-Host "    SELECT * FROM petel_schema.school_student_pricing_elements sspe" -ForegroundColor Cyan
Write-Host "    JOIN petel_schema.special_needs_pricing_elements snpe ON sspe.pricing_element_id = snpe.id" -ForegroundColor Cyan
Write-Host "    WHERE sspe.student_id = 204 AND snpe.element_name ILIKE '%guard%';" -ForegroundColor Cyan
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
