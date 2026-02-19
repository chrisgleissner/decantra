import { expect, test } from '@playwright/test';

const IgnoredConsolePatterns = [
  /autoplay/i,
  /audio.*gesture/i,
  /deprecated/i,
  /favicon/i
];

test('webgl smoke: loads, renders, and remains responsive after two taps', async ({ page }) => {
  const severeConsoleErrors: string[] = [];

  page.on('console', (msg) => {
    if (msg.type() !== 'error') {
      return;
    }

    const text = msg.text();
    if (IgnoredConsolePatterns.some((pattern) => pattern.test(text))) {
      return;
    }

    severeConsoleErrors.push(text);
  });

  await page.goto('/', { waitUntil: 'domcontentloaded' });

  const canvas = page.locator('canvas').first();
  await expect(canvas).toBeVisible({ timeout: 90000 });

  await page.waitForLoadState('networkidle', { timeout: 90000 });

  const canvasBox = await canvas.boundingBox();
  expect(canvasBox).not.toBeNull();

  if (!canvasBox) {
    return;
  }

  const firstTapX = canvasBox.x + canvasBox.width * 0.25;
  const firstTapY = canvasBox.y + canvasBox.height * 0.70;
  const secondTapX = canvasBox.x + canvasBox.width * 0.55;
  const secondTapY = canvasBox.y + canvasBox.height * 0.70;

  await page.mouse.click(firstTapX, firstTapY);
  await page.waitForTimeout(500);
  await page.mouse.click(secondTapX, secondTapY);
  await page.waitForTimeout(1000);

  await expect(canvas).toBeVisible();

  expect(severeConsoleErrors, severeConsoleErrors.join('\n')).toHaveLength(0);
});