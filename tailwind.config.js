/** @type {import('tailwindcss').Config} */

/* ---------------------------------------------------------------------------
   Design tokens de MisCatas (fase 2).

   Regla de oro: NUNCA se escribe un color suelto en una clase ni en un CSS.
   Todo pasa por estos tokens, de modo que un cambio de identidad se hace
   aquí y en ningún otro sitio. Los mismos tokens se exponen además como
   variables CSS en Styles/estilos.css, para poder usarlos desde los
   .razor.css aislados (donde las clases de Tailwind no llegan).

   Paleta
     burdeos  color de marca, fondos oscuros y cabeceras
     granate  variante viva, para acentos y estados activos
     dorado   champán: filetes, medallas, detalles de lujo
     crema    fondo de la aplicación y de las tarjetas
     carbon   texto principal
   --------------------------------------------------------------------------- */

module.exports = {
    content: [
        './src/MisCatas.Web/**/*.{razor,html,cshtml}',
        './src/MisCatas.Web/**/*.cs',
        './src/MisCatas.Web/wwwroot/**/*.html'
    ],
    theme: {
        extend: {
            colors: {
                burdeos: {
                    DEFAULT: '#6B1F2E',
                    50: '#FAF2F3',
                    100: '#F2DFE3',
                    200: '#E3BDC5',
                    300: '#CE8F9C',
                    400: '#B26173',
                    500: '#8C2F39',
                    600: '#6B1F2E',
                    700: '#561826',
                    800: '#3E111B',
                    900: '#280A11',
                    950: '#17050A'
                },
                granate: {
                    DEFAULT: '#8C2F39',
                    claro: '#A8414C',
                    oscuro: '#6F242C'
                },
                dorado: {
                    DEFAULT: '#C9A227',
                    50: '#FBF6E4',
                    100: '#F5E9BC',
                    200: '#EAD48B',
                    300: '#DBBC58',
                    400: '#C9A227',
                    500: '#A8861C',
                    600: '#866A14',
                    700: '#644F0F',
                    /* Para TEXTO dorado sobre crema hay que bajar mucho el tono:
                       el #C9A227 sobre #F7F3EE no alcanza el contraste AA. */
                    texto: '#6E5508'
                },
                crema: {
                    DEFAULT: '#F7F3EE',
                    claro: '#FDFBF8',
                    oscuro: '#EDE5DB',
                    borde: '#DFD4C6'
                },
                carbon: {
                    DEFAULT: '#1C1A1B',
                    /* Escala deliberadamente OSCURA: los grises claros hacían
                       ilegibles etiquetas y textos de apoyo sobre crema. El
                       tono más claro (400) supera 6.5:1 sobre #F7F3EE, así que
                       aguanta AA incluso en texto pequeño.
                       🔴 Debe coincidir con :root en Styles/estilos.css. */
                    800: '#211D1F',
                    700: '#2B2628',
                    600: '#383133',
                    500: '#4A4245',
                    400: '#5C5457'
                },
                /* Semánticos. Verificados con contraste AA sobre crema. */
                exito: { DEFAULT: '#1B6B35', suave: '#E6F3EA', texto: '#145128' },
                aviso: { DEFAULT: '#8A5A00', suave: '#FDF2DC', texto: '#6E4800' },
                peligro: { DEFAULT: '#A3161C', suave: '#FBE9E9', texto: '#8A1216' },
                info: { DEFAULT: '#1B4F8A', suave: '#E6EEF8', texto: '#163F6E' }
            },
            fontFamily: {
                /* Títulos: serif con carácter. Los fallbacks cubren el caso de
                   que no se hayan descargado las woff2 autoalojadas
                   (tools\descargar-fuentes.ps1). */
                serif: ['Playfair Display', 'Georgia', 'Cambria', 'Times New Roman', 'serif'],
                /* Datos y texto corrido. */
                sans: ['Inter', 'Segoe UI', 'system-ui', '-apple-system', 'sans-serif']
            },
            fontSize: {
                /* Escala fluida de la pantalla de escaparate (TV).
                   Pensada para leerse a 4-5 metros en 1920x1080. */
                'tv-sm': ['clamp(1.25rem, 1.6vw, 2rem)', { lineHeight: '1.3' }],
                'tv-md': ['clamp(1.75rem, 2.4vw, 3rem)', { lineHeight: '1.2' }],
                'tv-lg': ['clamp(2.5rem, 4vw, 5rem)', { lineHeight: '1.1' }],
                'tv-xl': ['clamp(3.5rem, 6vw, 7.5rem)', { lineHeight: '1' }],
                'tv-2xl': ['clamp(5rem, 11vw, 14rem)', { lineHeight: '0.95' }],
                /* Número de vino gigante en la votación del móvil. */
                cifra: ['clamp(4rem, 26vw, 7rem)', { lineHeight: '1' }]
            },
            spacing: {
                /* Objetivo táctil mínimo exigido por la fase 2: 48 px. */
                tactil: '3rem',
                'tactil-lg': '3.5rem'
            },
            minHeight: {
                tactil: '3rem',
                'tactil-lg': '3.5rem'
            },
            minWidth: {
                tactil: '3rem'
            },
            borderRadius: {
                copa: '1.25rem',
                etiqueta: '0.625rem'
            },
            boxShadow: {
                /* Sombras cálidas: tiradas al burdeos, no al negro puro. */
                tarjeta: '0 1px 2px rgba(28, 26, 27, 0.05), 0 6px 20px -8px rgba(107, 31, 46, 0.18)',
                'tarjeta-alta': '0 2px 4px rgba(28, 26, 27, 0.06), 0 18px 40px -12px rgba(107, 31, 46, 0.28)',
                copa: '0 20px 50px -20px rgba(107, 31, 46, 0.55)',
                dorada: '0 0 0 1px rgba(201, 162, 39, 0.45), 0 10px 30px -14px rgba(201, 162, 39, 0.5)',
                hundida: 'inset 0 2px 6px rgba(28, 26, 27, 0.12)'
            },
            backgroundImage: {
                /* Textura de fondo muy sutil en SVG embebido: no hay que servir
                   ninguna imagen y no se percibe como "ruido". */
                textura:
                    "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='120' height='120' viewBox='0 0 120 120'%3E%3Cg fill='none' stroke='%236B1F2E' stroke-opacity='0.05'%3E%3Ccircle cx='20' cy='20' r='9'/%3E%3Ccircle cx='80' cy='50' r='9'/%3E%3Ccircle cx='45' cy='95' r='9'/%3E%3Ccircle cx='105' cy='100' r='9'/%3E%3C/g%3E%3C/svg%3E\")",
                'degradado-vino': 'linear-gradient(140deg, #6B1F2E 0%, #8C2F39 55%, #561826 100%)',
                'degradado-noche': 'linear-gradient(160deg, #280A11 0%, #561826 45%, #1C1A1B 100%)',
                'filete-dorado': 'linear-gradient(90deg, rgba(201,162,39,0) 0%, #C9A227 50%, rgba(201,162,39,0) 100%)'
            },
            transitionTimingFunction: {
                copa: 'cubic-bezier(0.22, 1, 0.36, 1)'
            },
            keyframes: {
                'aparecer-abajo': {
                    '0%': { opacity: '0', transform: 'translateY(0.75rem)' },
                    '100%': { opacity: '1', transform: 'translateY(0)' }
                },
                aparecer: {
                    '0%': { opacity: '0' },
                    '100%': { opacity: '1' }
                },
                'brillo-dorado': {
                    '0%, 100%': { opacity: '0.35' },
                    '50%': { opacity: '1' }
                },
                'latido-alerta': {
                    '0%, 100%': { transform: 'scale(1)' },
                    '50%': { transform: 'scale(1.06)' }
                },
                descorchar: {
                    '0%': { transform: 'translateY(0) rotate(0deg)' },
                    '60%': { transform: 'translateY(-0.4rem) rotate(-6deg)' },
                    '100%': { transform: 'translateY(0) rotate(0deg)' }
                },
                servir: {
                    '0%': { transform: 'scaleY(0)', transformOrigin: 'bottom' },
                    '100%': { transform: 'scaleY(1)', transformOrigin: 'bottom' }
                }
            },
            animation: {
                'aparecer-abajo': 'aparecer-abajo 0.35s cubic-bezier(0.22, 1, 0.36, 1) both',
                aparecer: 'aparecer 0.3s ease-out both',
                'brillo-dorado': 'brillo-dorado 2.4s ease-in-out infinite',
                'latido-alerta': 'latido-alerta 1s ease-in-out infinite',
                descorchar: 'descorchar 0.6s cubic-bezier(0.22, 1, 0.36, 1)',
                servir: 'servir 0.8s cubic-bezier(0.22, 1, 0.36, 1) both'
            }
        }
    },
    plugins: []
};
