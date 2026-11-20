import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { App } from './app';
import { routes } from './app.routes';
import { DemoSession } from './auth/demo-session';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    TestBed.inject(DemoSession).logout();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('redirects the root to the login page', async () => {
    const harness = await RouterTestingHarness.create('/');
    expect(TestBed.inject(Router).url).toBe('/home');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Accedi a GYI');
  });

  it('shows the saved visit order and lets the editor confirm the proposal', async () => {
    const session = TestBed.inject(DemoSession);
    session.login('editor', 'editor');
    const itineraryId = '001dd95b-afde-41ec-b1db-701112bf34fa';
    const url = '/editor/' + itineraryId;
    const harness = await RouterTestingHarness.create(url);
    const http = TestBed.inject(HttpTestingController);
    const itinerary = {
      id: itineraryId,
      title: 'Percorso nelle Marche',
      theme: 'Arte',
      description: 'Un itinerario tra musei.',
      geographicArea: 'Marche',
      stops: [
        { id: '2', name: 'Seconda nel catalogo', municipality: null },
        { id: '1', name: 'Prima nel catalogo', municipality: null },
      ],
      segments: [],
    };
    const endpoint = '/api/itineraries/' + itineraryId;
    http.expectOne(endpoint).flush({ ...itinerary, status: 'pending' });
    harness.detectChanges();

    expect(TestBed.inject(Router).url).toBe(url);
    const stops = harness.routeNativeElement?.querySelectorAll('ol li');
    expect(Array.from(stops ?? [], (stop) => stop.textContent?.trim())).toEqual([
      'Seconda nel catalogo',
      'Prima nel catalogo',
    ]);
    const page = harness.routeNativeElement!;
    expect(page.textContent).toContain('In attesa di conferma');
    const findButton = (label: string) =>
      Array.from(page.querySelectorAll('button')).find(
        (button) => button.textContent?.trim() === label,
      );
    findButton('Conferma itinerario')!.click();
    http.expectOne({ method: 'PUT', url: endpoint + '/confirm' }).flush(null, {
      status: 204,
      statusText: 'No Content',
    });
    http.expectOne(endpoint).flush({ ...itinerary, status: 'confirmed' });
    harness.detectChanges();

    expect(page.textContent).toContain('Confermato');
    expect(findButton('Conferma itinerario')).toBeUndefined();
    expect(findButton('Modifica tappe')).toBeUndefined();
  });

  it.each([
    { scenario: 'without stops', expectedIds: [] },
    { scenario: 'with one stop', expectedIds: ['1'] },
    { scenario: 'with reordered stops', expectedIds: ['3', '1'] },
  ])('saves a draft $scenario and loads its detail from the URL', async ({ expectedIds }) => {
    const session = TestBed.inject(DemoSession);
    session.login('editor', 'editor');
    const harness = await RouterTestingHarness.create('/editor/' + session.id);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/itineraries/mine').flush([]);
    harness.detectChanges();

    const page = harness.routeNativeElement!;
    const click = (label: string) => {
      const button = Array.from(page.querySelectorAll('button')).find(
        (element) => (element.getAttribute('aria-label') ?? element.textContent?.trim()) === label,
      );
      expect(button).toBeDefined();
      button!.click();
      harness.detectChanges();
    };

    click('Nuovo itinerario');
    const catalog = [
      { id: '1', name: 'Museo', municipality: 'Ancona', canAddToItinerary: true },
      { id: '2', name: 'Abbazia', municipality: 'Fiastra', canAddToItinerary: true },
      { id: '3', name: 'Rocca', municipality: 'Urbino', canAddToItinerary: true },
    ];
    http.expectOne({ method: 'GET', url: '/api/stops' }).flush([
      ...catalog,
      {
        id: '4',
        name: 'Parco',
        municipality: null,
        description: 'Proposta in attesa',
        latitude: 43.5,
        longitude: 13.5,
        canAddToItinerary: false,
      },
    ]);
    harness.detectChanges();

    for (const id of ['title', 'theme', 'description', 'geographicArea']) {
      const field = page.querySelector<HTMLInputElement>('#' + id)!;
      field.value = 'Prova';
      field.dispatchEvent(new Event('input', { bubbles: true }));
    }
    const catalogMenu = page.querySelector<HTMLSelectElement>('#catalog-stop')!;
    catalogMenu.value = '4';
    catalogMenu.dispatchEvent(new Event('change', { bubbles: true }));
    harness.detectChanges();
    expect(page.textContent).toContain('Proposta in attesa');
    expect(page.textContent).toContain('Latitudine 43.5, longitudine 13.5');
    const addButton = Array.from(page.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Aggiungi tappa',
    );
    expect(addButton?.disabled).toBe(true);
    const addStop = (id: string) => {
      catalogMenu.value = id;
      catalogMenu.dispatchEvent(new Event('change', { bubbles: true }));
      harness.detectChanges();
      click('Aggiungi tappa');
    };
    if (expectedIds.length === 1) {
      addStop('1');
    }
    if (expectedIds.length === 2) {
      catalog.forEach((stop) => addStop(stop.id));
      click('Rimuovi: Abbazia');
      click('Sposta prima: Rocca');
    }
    click('Proponi itinerario');

    const create = http.expectOne({ method: 'POST', url: '/api/itineraries' });
    expect(create.request.body.stopIds).toEqual(expectedIds);
    const itinerary = {
      id: '001dd95b-afde-41ec-b1db-701112bf34fa',
      title: 'Prova',
      status: 'pending',
    };
    create.flush(itinerary, { status: 201, statusText: 'Created' });
    await harness.fixture.whenStable();

    const savedStops = expectedIds.map((id) => catalog.find((stop) => stop.id === id)!);
    http.expectOne('/api/itineraries/' + itinerary.id).flush({
      ...itinerary,
      stops: savedStops,
      segments: [],
    });
    harness.detectChanges();
    expect(TestBed.inject(Router).url).toBe('/editor/' + itinerary.id);
    expect(Array.from(page.querySelectorAll('ol li'), (stop) => stop.textContent?.trim())).toEqual(
      savedStops.map((stop) => stop.name + ' — ' + stop.municipality),
    );
  });
});
