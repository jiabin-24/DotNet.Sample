import { test, expect } from '@playwright/test';

test('test', async ({ page }) => {
  await page.goto('https://www.bing.com/?toWww=1&redig=28E972B4C05144379762F91F82661CD6');
  await page.getByRole('combobox', { name: 'Enter your search here -' }).click();
  await page.getByRole('combobox', { name: 'Enter your search here -' }).fill('monmenta.ai');
  await page.goto('https://www.bing.com/search?q=monmenta.ai&form=QBLH&sp=-1&ghc=1&lq=0&pq=monmenta.ai&sc=0-11&qs=n&sk=&cvid=0BF3EDDF9E3B48E0B78B1AA7D85E3007');
  const page1Promise = page.waitForEvent('popup');
  await page.getByRole('link', { name: 'Momenta | Building Autonomous' }).click();
  const page1 = await page1Promise;
  await expect(page1.getByRole('banner')).toContainText('Technology');
});